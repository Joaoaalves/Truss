using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Truss.Cli.Templates;

namespace Truss.Cli
{
    /// <summary>
    /// Extracts a bounded context into its own service: a host project with its
    /// own composition root and, by default, its own database. The context's
    /// projects are reused as they are; the split moves the context's routes
    /// and registrations from the monolith's Program into the new host and
    /// unwires the context from the monolith. The handlers never change.
    /// </summary>
    internal static class ServiceSplitter
    {
        public static int Split(TrussManifest manifest, string root, string context, bool sharedDatabase, Action<string> log)
        {
            if (!manifest.UsesEntityFramework)
                throw new ArgumentException("Splitting a service needs a database. Scaffold the project with --database first.");

            if (Directory.Exists(Path.Combine(root, "src", $"{manifest.Name}.{context}.Api")))
            {
                log($"The {context} context is already split into its own service.");
                return 0;
            }

            // The split builds on the project layout; converting first is the
            // same mechanical move the user could run themselves.
            if (!ContextProjects.Exists(manifest, root, context))
            {
                if (!Directory.Exists(Path.Combine(root, manifest.DomainProject, context))
                    && !Directory.Exists(Path.Combine(root, manifest.ApplicationProject, context)))
                {
                    throw new ArgumentException($"No {context} context was found in the project.");
                }

                ContextProjects.Create(manifest, root, context, log);
            }

            var moved = ExtractFromProgram(manifest, root, context);

            WriteService(manifest, root, context, sharedDatabase, moved, log);
            WriteContracts(manifest, root, context, log);
            UnwireMonolith(manifest, root, context, sharedDatabase, log);
            PatchTests(manifest, root, context, sharedDatabase);

            manifest.Settings[$"service.{context}"] = sharedDatabase ? "shared-db" : "own-db";
            manifest.Save(root);
            DockerScaffolder.WriteHostDockerfiles(manifest, root, log);
            AgentsGenerator.Write(manifest, root);

            manifest.Settings.TryGetValue("messaging.transport", out var transport);

            if (manifest.Modules.Contains("messaging") && transport is null or "inmemory")
                log("WARNING: the inmemory transport does not cross processes. Events between the monolith and the service need a durable transport: postgres, rabbitmq or redis.");

            if (!sharedDatabase)
                log($"The service owns its database now. The monolith's next migration drops the {context} tables; move the data before applying it.");

            log($"The {context} service lives at src/{manifest.Name}.{context}.Api. Run it with: dotnet run --project src/{manifest.Name}.{context}.Api");
            log($"To query it synchronously from another service, put the query in the Contracts project and register: services.AddRemoteContext<{context}Contracts>(\"{context}\", new Uri(\"http://localhost:<port>\")); (package Truss.Remote)");
            log("The handlers did not change; only the hosting did.");

            return 0;
        }

        private sealed record MovedLines(List<string> Usings, List<string> Services, List<string> Endpoints);

        /// <summary>
        /// Lifts every line of the monolith's Program that belongs to the
        /// context: its usings, its service registrations and its routes. The
        /// types are read from the context's project files, the same source
        /// truss remove uses, so generated slices move completely.
        /// </summary>
        private static MovedLines ExtractFromProgram(TrussManifest manifest, string root, string context)
        {
            var program = Path.Combine(root, manifest.ApiProject, "Program.cs");
            var types = ContextTypes(manifest, root, context);
            var moved = new MovedLines([], [], []);
            var kept = new List<string>();

            foreach (var line in File.ReadAllLines(program))
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith($"using {manifest.Name}.", StringComparison.Ordinal)
                    && (trimmed.Contains($".{context};", StringComparison.Ordinal) || trimmed.Contains($".{context}.", StringComparison.Ordinal)))
                {
                    moved.Usings.Add(trimmed);
                    continue;
                }

                if (trimmed.Contains($"AddAssembly<{context}AssemblyMarker>", StringComparison.Ordinal))
                    continue;

                if (ReferencesAny(trimmed, types))
                {
                    if (trimmed.StartsWith("app.Map", StringComparison.Ordinal))
                    {
                        moved.Endpoints.Add(trimmed);
                        continue;
                    }

                    if (trimmed.StartsWith("builder.Services.", StringComparison.Ordinal))
                    {
                        moved.Services.Add(trimmed);
                        continue;
                    }
                }

                kept.Add(line);
            }

            File.WriteAllLines(program, kept);
            return moved;
        }

        private static void WriteService(TrussManifest manifest, string root, string context, bool sharedDatabase, MovedLines moved, Action<string> log)
        {
            var directory = Path.Combine(root, "src", $"{manifest.Name}.{context}.Api");
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(Path.Combine(directory, "Properties"));

            File.WriteAllText(Path.Combine(directory, $"{manifest.Name}.{context}.Api.csproj"), BuildCsproj(manifest, context) + Environment.NewLine);
            File.WriteAllText(Path.Combine(directory, "Program.cs"), BuildProgram(manifest, context, sharedDatabase, moved) + Environment.NewLine);
            File.WriteAllText(Path.Combine(directory, $"{context}DbContext.cs"), BuildDbContext(manifest, context) + Environment.NewLine);
            File.WriteAllText(Path.Combine(directory, "appsettings.json"), BuildAppSettings(manifest, root, context, sharedDatabase) + Environment.NewLine);
            File.WriteAllText(Path.Combine(directory, "Properties", "launchSettings.json"), BuildLaunchSettings(manifest, root, context) + Environment.NewLine);

            var solution = Path.Combine(root, $"{manifest.Name}.slnx");
            var entry = $"""    <Project Path="src/{manifest.Name}.{context}.Api/{manifest.Name}.{context}.Api.csproj" />""";

            if (!SourceEditor.InsertBefore(solution, "  </Folder>", entry))
                log($"Could not update the solution automatically. Add to {manifest.Name}.slnx: {entry.Trim()}");
        }

        private static string BuildCsproj(TrussManifest manifest, string context)
        {
            var packages = new StringBuilder();

            void Package(string id, string version, bool developmentDependency = false) => packages
                .Append("    <PackageReference Include=\"").Append(id).Append("\" Version=\"").Append(version).Append('"')
                .Append(developmentDependency ? " PrivateAssets=\"all\"" : string.Empty)
                .AppendLine(" />");

            Package("Truss.Application", manifest.TrussVersion);
            Package("Truss.AspNetCore", manifest.TrussVersion);
            Package("Truss.Generators", manifest.TrussVersion, developmentDependency: true);
            Package(ProjectScaffolder.ProviderPackage(manifest.Database), "10.*");

            if (manifest.Database == "sqlite")
                Package("SQLitePCLRaw.bundle_e_sqlite3", "3.*");

            Package("Microsoft.AspNetCore.OpenApi", "10.*");
            Package("Microsoft.OpenApi", "2.*");
            Package("Microsoft.EntityFrameworkCore.Design", "10.*", developmentDependency: true);
            Package("Scalar.AspNetCore", "2.*");

            if (manifest.Modules.Contains("messaging"))
            {
                Package("Truss.Messaging", manifest.TrussVersion);
                Package("Truss.Messaging.EntityFrameworkCore", manifest.TrussVersion);
                Package("Truss.Messaging.AspNetCore", manifest.TrussVersion);

                manifest.Settings.TryGetValue("messaging.transport", out var transport);

                if (transport == "postgres")
                    Package("Truss.Messaging.Postgres", manifest.TrussVersion);
                else if (transport == "rabbitmq")
                    Package("Truss.Messaging.RabbitMq", manifest.TrussVersion);
                else if (transport == "redis")
                    Package("Truss.Messaging.Redis", manifest.TrussVersion);
            }

            if (manifest.Modules.Contains("jobs"))
            {
                Package("Truss.Jobs", manifest.TrussVersion);
                Package("Truss.Jobs.EntityFrameworkCore", manifest.TrussVersion);
                Package("Truss.Jobs.AspNetCore", manifest.TrussVersion);
            }

            if (manifest.Modules.Contains("email"))
            {
                Package("Truss.Email", manifest.TrussVersion);

                if (manifest.Settings.TryGetValue("email.provider", out var email) && email == "resend")
                    Package("Truss.Email.Resend", manifest.TrussVersion);
            }

            if (manifest.Modules.Contains("observability"))
            {
                Package("Truss.Observability", manifest.TrussVersion);
                Package("Truss.Observability.AspNetCore", manifest.TrussVersion);

                if (manifest.Settings.ContainsKey("observability.dashboard"))
                    Package("Truss.Observability.OpenTelemetry", manifest.TrussVersion);
            }

            if (manifest.Modules.Contains("auth"))
                Package("Truss.Auth.Jwt", manifest.TrussVersion);

            var references = new StringBuilder();

            references.Append("    <ProjectReference Include=\"..\\").Append(manifest.Name).Append('.').Append(context)
                .Append(".Infrastructure\\").Append(manifest.Name).Append('.').Append(context).AppendLine(".Infrastructure.csproj\" />");

            if (Contracts(manifest))
            {
                references.Append("    <ProjectReference Include=\"..\\").Append(manifest.Name).Append('.').Append(context)
                    .Append(".Contracts\\").Append(manifest.Name).Append('.').Append(context).AppendLine(".Contracts.csproj\" />");
            }

            return $"""
                <Project Sdk="Microsoft.NET.Sdk.Web">

                  <ItemGroup>
                {references.ToString().TrimEnd()}
                  </ItemGroup>

                  <ItemGroup>
                {packages.ToString().TrimEnd()}
                  </ItemGroup>

                </Project>
                """;
        }

        private static string BuildProgram(TrussManifest manifest, string context, bool sharedDatabase, MovedLines moved)
        {
            var name = manifest.Name;
            var program = new StringBuilder();

            var usings = new SortedSet<string>(StringComparer.Ordinal)
            {
                $"using {name}.Application.{context};",
                $"using {name}.Infrastructure.{context};",
                $"using {name}.{context}.Api;",
                $"using {name}.{context}.Contracts;",
                "using Microsoft.EntityFrameworkCore;",
                "using Scalar.AspNetCore;",
                "using Truss.Application;"
            };

            foreach (var line in moved.Usings)
                usings.Add(line);

            foreach (var line in usings)
                program.AppendLine(line);

            program.AppendLine();
            program.AppendLine("var builder = WebApplication.CreateBuilder(args);");
            program.AppendLine();
            program.AppendLine($"""
                builder.Services.AddDbContext<{context}DbContext>(options =>
                    options.{ProjectScaffolder.ProviderMethod(manifest.Database)}(builder.Configuration.GetConnectionString("Default")));
                """);
            program.AppendLine();
            program.AppendLine($$"""
                builder.Services.AddTruss(options =>
                {
                    options.AddAssembly<{{context}}AssemblyMarker>();
                });
                """);
            program.AppendLine();
            program.AppendLine($"builder.Services.AddTrussEntityFramework<{context}DbContext>();");
            program.AppendLine("builder.Services.AddOpenApi();");
            program.AppendLine($"builder.Services.AddHealthChecks().AddTrussDatabase<{context}DbContext>();");

            if (manifest.Modules.Contains("messaging"))
            {
                manifest.Settings.TryGetValue("messaging.transport", out var transport);

                program.AppendLine();
                program.AppendLine($$"""
                    builder.Services.AddTrussMessaging(options =>
                    {
                        options.AddAssembly<{{context}}AssemblyMarker>();
                    });
                    """);
                program.AppendLine();
                program.AppendLine(ModuleInstaller.TransportRegistration(transport ?? "inmemory"));
                program.AppendLine();
                program.AppendLine($"builder.Services.AddTrussOutbox<{context}DbContext>();");
                program.AppendLine($"builder.Services.AddTrussInbox<{context}DbContext>();");
            }

            if (manifest.Modules.Contains("jobs"))
            {
                program.AppendLine();
                program.AppendLine($$"""
                    builder.Services.AddTrussJobs(options =>
                    {
                        options.AddAssembly<{{context}}AssemblyMarker>();
                    });
                    """);
                program.AppendLine();
                program.AppendLine($"builder.Services.AddTrussJobsEntityFramework<{context}DbContext>();");
            }

            if (manifest.Modules.Contains("email"))
            {
                manifest.Settings.TryGetValue("email.provider", out var email);
                program.AppendLine();
                program.AppendLine(ModuleInstaller.EmailRegistration(email ?? "console"));
                program.AppendLine("builder.Services.AddTrussEmailValidation(options => builder.Configuration.GetSection(\"Truss:Email:Validation\").Bind(options));");
            }

            if (manifest.Modules.Contains("observability"))
            {
                program.AppendLine();
                program.AppendLine("builder.Services.AddTrussObservability();");

                if (manifest.Settings.ContainsKey("observability.dashboard"))
                    program.AppendLine("builder.Services.AddTrussOpenTelemetry();");
            }

            if (manifest.Modules.Contains("auth"))
            {
                // The same issuer, audience and signing key as the monolith, so
                // one login works across the whole constellation.
                program.AppendLine();
                program.AppendLine("""
                    builder.Services.AddTrussJwtAuth(options =>
                    {
                        options.Issuer = builder.Configuration["Truss:Auth:Jwt:Issuer"]!;
                        options.Audience = builder.Configuration["Truss:Auth:Jwt:Audience"]!;
                        options.SigningKey = builder.Configuration["Truss:Auth:Jwt:SigningKey"]!;
                    });
                    """);
            }

            if (moved.Services.Count > 0)
            {
                program.AppendLine();

                foreach (var line in moved.Services)
                    program.AppendLine(line);
            }

            program.AppendLine();
            program.AppendLine("// truss: services");
            program.AppendLine();
            program.AppendLine("var app = builder.Build();");
            program.AppendLine();

            if (manifest.Modules.Contains("observability"))
                program.AppendLine("app.UseTrussCorrelation();");

            if (manifest.Modules.Contains("auth"))
            {
                program.AppendLine("app.UseAuthentication();");
                program.AppendLine("app.UseAuthorization();");
            }

            program.AppendLine();
            program.AppendLine("// truss: middleware");
            program.AppendLine();

            if (sharedDatabase)
            {
                program.AppendLine("// The monolith owns the shared database schema; this service never migrates it.");
            }
            else
            {
                program.AppendLine("""
                    if (app.Environment.IsDevelopment())
                    {
                        using var scope = app.Services.CreateScope();
                    """);
                program.AppendLine($"    var database = scope.ServiceProvider.GetRequiredService<{context}DbContext>().Database;");
                program.AppendLine("""

                        if (database.GetMigrations().Any())
                            database.Migrate();
                        else
                            database.EnsureCreated();
                    }
                    """);
            }

            program.AppendLine();
            program.AppendLine("if (app.Environment.IsDevelopment())");
            program.AppendLine("{");
            program.AppendLine("    app.MapOpenApi();");
            program.AppendLine("    app.MapScalarApiReference();");
            program.AppendLine("}");
            program.AppendLine();
            program.AppendLine("app.MapHealthChecks(\"/health\");");
            program.AppendLine($"app.MapGet(\"/\", () => \"{manifest.Name}.{context} is running.\");");

            if (manifest.Modules.Contains("jobs"))
                program.AppendLine("app.MapTrussJobs();");

            if (manifest.Modules.Contains("messaging"))
                program.AppendLine("app.MapTrussOutbox();");

            program.AppendLine($"app.MapRemoteContext(typeof({context}Contracts).Assembly);");

            if (moved.Endpoints.Count > 0)
            {
                program.AppendLine();

                foreach (var line in moved.Endpoints)
                    program.AppendLine(line);
            }

            program.AppendLine();
            program.AppendLine("// truss: endpoints");
            program.AppendLine();
            program.Append("app.Run();");

            return program.ToString();
        }

        private static string BuildDbContext(TrussManifest manifest, string context)
        {
            var extras = new StringBuilder();

            if (manifest.Modules.Contains("messaging"))
            {
                extras.Append(Environment.NewLine).Append("            modelBuilder.ApplyTrussOutbox();");
                extras.Append(Environment.NewLine).Append("            modelBuilder.ApplyTrussInbox();");
            }

            if (manifest.Modules.Contains("jobs"))
                extras.Append(Environment.NewLine).Append("            modelBuilder.ApplyTrussJobs();");

            return ServiceTemplates.DbContext
                .Replace("__MODEL_EXTRAS__", extras.ToString())
                .Replace("__CONTEXT__", context)
                .Replace("__NAME__", manifest.Name);
        }

        /// <summary>
        /// The service starts from the monolith's settings, so every section a
        /// module wrote (transports, email, the JWT key that must match across
        /// the constellation) carries over; only the default connection string
        /// changes when the service owns its database.
        /// </summary>
        private static string BuildAppSettings(TrussManifest manifest, string root, string context, bool sharedDatabase)
        {
            var main = Path.Combine(root, manifest.ApiProject, "appsettings.json");
            var settings = JsonNode.Parse(File.ReadAllText(main))!.AsObject();

            if (!sharedDatabase && settings["ConnectionStrings"] is JsonObject connections && connections["Default"] is { } current)
                connections["Default"] = OwnDatabase(current.GetValue<string>(), manifest, context);

            return settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private static string OwnDatabase(string connectionString, TrussManifest manifest, string context)
        {
            var suffix = context.ToLowerInvariant();

            return manifest.Database switch
            {
                "sqlite" => $"Data Source={manifest.Name.ToLowerInvariant()}-{suffix}.db",
                _ => Regex.IsMatch(connectionString, "Database=([^;]+)", RegexOptions.IgnoreCase)
                    ? Regex.Replace(connectionString, "Database=([^;]+)", $"Database=$1_{suffix}", RegexOptions.IgnoreCase)
                    : connectionString
            };
        }

        private static string BuildLaunchSettings(TrussManifest manifest, string root, string context)
        {
            // The first service answers at 5100, the next at 5101, and so on;
            // the directory of the service being split is already on disk, so
            // it does not count itself.
            var others = Directory.EnumerateDirectories(Path.Combine(root, "src"), $"{manifest.Name}.*.Api")
                .Count(directory => Path.GetFileName(directory) != $"{manifest.Name}.{context}.Api");

            return ServiceTemplates.LaunchSettings
                .Replace("__PORT__", (5100 + others).ToString())
                .Replace("__CONTEXT__", context)
                .Replace("__NAME__", manifest.Name);
        }

        /// <summary>
        /// The contracts project is where the context's events go the moment
        /// another service consumes them, so no service ever references
        /// another's internals. Both sides reference it from day one.
        /// </summary>
        private static void WriteContracts(TrussManifest manifest, string root, string context, Action<string> log)
        {
            if (!Contracts(manifest))
                return;

            var directory = Path.Combine(root, "src", $"{manifest.Name}.{context}.Contracts");

            if (Directory.Exists(directory))
                return;

            Directory.CreateDirectory(directory);

            var messaging = manifest.Modules.Contains("messaging")
                ? $"{Environment.NewLine}    <PackageReference Include=\"Truss.Messaging.Abstractions\" Version=\"{manifest.TrussVersion}\" />"
                : string.Empty;

            string Render(string template) => template
                .Replace("__MESSAGING_PACKAGE__", messaging)
                .Replace("__TRUSS_VERSION__", manifest.TrussVersion)
                .Replace("__CONTEXTLOWER__", context.ToLowerInvariant())
                .Replace("__CONTEXT__", context)
                .Replace("__NAME__", manifest.Name);

            File.WriteAllText(Path.Combine(directory, $"{manifest.Name}.{context}.Contracts.csproj"), Render(ServiceTemplates.ContractsCsproj) + Environment.NewLine);
            File.WriteAllText(Path.Combine(directory, $"{context}Contracts.cs"), Render(ServiceTemplates.ContractsReadme) + Environment.NewLine);

            var solution = Path.Combine(root, $"{manifest.Name}.slnx");
            var entry = $"""    <Project Path="src/{manifest.Name}.{context}.Contracts/{manifest.Name}.{context}.Contracts.csproj" />""";
            SourceEditor.InsertBefore(solution, "  </Folder>", entry);

            CsprojEditor.AddProjectReference(
                Csproj(root, manifest.ApplicationProject),
                $"..\\{manifest.Name}.{context}.Contracts\\{manifest.Name}.{context}.Contracts.csproj");

            CsprojEditor.AddProjectReference(
                Path.Combine(root, "src", $"{manifest.Name}.{context}.Application", $"{manifest.Name}.{context}.Application.csproj"),
                $"..\\{manifest.Name}.{context}.Contracts\\{manifest.Name}.{context}.Contracts.csproj");

            log($"Events other services consume go into src/{manifest.Name}.{context}.Contracts; both sides already reference it.");
        }

        /// <summary>
        /// The monolith stops hosting the context: the assembly registrations
        /// leave both composition roots, and with an owned database the model
        /// and the project reference leave the main infrastructure too. With a
        /// shared database the monolith keeps the model, because it keeps
        /// owning the schema.
        /// </summary>
        private static void UnwireMonolith(TrussManifest manifest, string root, string context, bool sharedDatabase, Action<string> log)
        {
            RemoveMarkerLines(Path.Combine(root, manifest.ApiProject, "Program.cs"), manifest, context);
            RemoveMarkerLines(Path.Combine(root, "src", $"{manifest.Name}.Worker", "Program.cs"), manifest, context);

            if (sharedDatabase)
                return;

            var appDbContext = Path.Combine(root, manifest.InfrastructureProject, "AppDbContext.cs");

            if (File.Exists(appDbContext))
            {
                var lines = File.ReadAllLines(appDbContext)
                    .Where(line => !line.Contains($"{context}InfrastructureMarker", StringComparison.Ordinal)
                        && line.Trim() != $"using {manifest.Name}.Infrastructure.{context};")
                    .ToArray();

                File.WriteAllLines(appDbContext, lines);
            }

            var infrastructureCsproj = Csproj(root, manifest.InfrastructureProject);
            var kept = File.ReadAllLines(infrastructureCsproj)
                .Where(line => !line.Contains($"{manifest.Name}.{context}.Infrastructure", StringComparison.Ordinal))
                .ToArray();

            File.WriteAllLines(infrastructureCsproj, kept);
        }

        private static void RemoveMarkerLines(string program, TrussManifest manifest, string context)
        {
            if (!File.Exists(program))
                return;

            var lines = File.ReadAllLines(program)
                .Where(line => !line.Contains($"AddAssembly<{context}AssemblyMarker>", StringComparison.Ordinal)
                    && line.Trim() != $"using {manifest.Name}.Application.{context};")
                .ToArray();

            File.WriteAllLines(program, lines);
        }

        /// <summary>
        /// The context's integration tests keep living in the shared test
        /// project, but with an owned database their host must boot the
        /// service's DbContext, where the context's tables now live.
        /// </summary>
        private static void PatchTests(TrussManifest manifest, string root, string context, bool sharedDatabase)
        {
            var tests = Path.Combine(root, manifest.IntegrationTestsProject, context);

            if (sharedDatabase || !manifest.Tests || !Directory.Exists(tests))
                return;

            CsprojEditor.AddProjectReference(
                Csproj(root, manifest.IntegrationTestsProject),
                $"..\\..\\src\\{manifest.Name}.{context}.Api\\{manifest.Name}.{context}.Api.csproj");

            foreach (var file in Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);

                if (!content.Contains("TrussTestHost.Start<AppDbContext>"))
                    continue;

                content = content.Replace("TrussTestHost.Start<AppDbContext>", $"TrussTestHost.Start<{context}DbContext>");

                var usingLine = $"using {manifest.Name}.{context}.Api;";

                if (!content.Contains(usingLine))
                    content = content.Replace($"using {manifest.Name}.Infrastructure;", $"using {manifest.Name}.Infrastructure;{Environment.NewLine}{usingLine}");

                File.WriteAllText(file, content);
            }
        }

        private static bool Contracts(TrussManifest manifest)
        {
            // Events and synchronous queries both travel through the contracts
            // project, so every service gets one.
            return true;
        }

        private static HashSet<string> ContextTypes(TrussManifest manifest, string root, string context)
        {
            var directories = new[] { "Domain", "Application", "Infrastructure" }
                .Select(layer => Path.Combine(root, "src", $"{manifest.Name}.{context}.{layer}"))
                .Where(Directory.Exists);

            return directories
                .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);
        }

        private static bool ReferencesAny(string line, HashSet<string> types)
        {
            return types.Any(type => Regex.IsMatch(line, $@"\b{Regex.Escape(type)}\b"));
        }

        private static string Csproj(string root, string projectDirectory)
        {
            return Directory.EnumerateFiles(Path.Combine(root, projectDirectory), "*.csproj").First();
        }
    }
}
