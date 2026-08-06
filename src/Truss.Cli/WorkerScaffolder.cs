using System.Text;

namespace Truss.Cli
{
    internal static class WorkerScaffolder
    {
        public static int Install(TrussManifest manifest, string root, Action<string> log)
        {
            if (!manifest.Modules.Contains("messaging"))
            {
                log("The worker consumes messages and jobs and requires messaging. Run: truss add messaging");
                return 1;
            }

            var workerProject = Path.Combine("src", $"{manifest.Name}.Worker");
            var directory = Path.Combine(root, workerProject);

            if (Directory.Exists(directory))
            {
                log("A worker project already exists; refusing to overwrite it.");
                return 1;
            }

            Directory.CreateDirectory(directory);

            File.WriteAllText(Path.Combine(directory, $"{manifest.Name}.Worker.csproj"), BuildCsproj(manifest) + Environment.NewLine);
            File.WriteAllText(Path.Combine(directory, "Program.cs"), BuildProgram(manifest) + Environment.NewLine);
            File.WriteAllText(Path.Combine(directory, "appsettings.json"), BuildAppSettings(manifest) + Environment.NewLine);

            AddToSolution(manifest, root, workerProject, log);

            log("The worker was scaffolded. It consumes the same messages and jobs as the API; run it with dotnet run --project " + workerProject);

            if (manifest.Database == "sqlite")
                log("Note: with sqlite each process opens its own database file. Use postgres or sqlserver when the API and the worker must share state.");

            return 0;
        }

        private static string BuildCsproj(TrussManifest manifest)
        {
            var infrastructureReference = manifest.UsesEntityFramework
                ? $"""
                       <ProjectReference Include="..\{manifest.Name}.Infrastructure\{manifest.Name}.Infrastructure.csproj" />
                   """
                : $"""
                       <ProjectReference Include="..\{manifest.Name}.Application\{manifest.Name}.Application.csproj" />
                   """;

            return $"""
                <Project Sdk="Microsoft.NET.Sdk.Worker">

                  <ItemGroup>
                {infrastructureReference}
                  </ItemGroup>

                  <ItemGroup>
                    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.*" />
                    <PackageReference Include="Truss.Application" Version="{manifest.TrussVersion}" />
                    <PackageReference Include="Truss.Generators" Version="{manifest.TrussVersion}" PrivateAssets="all" />
                  </ItemGroup>

                </Project>
                """;
        }

        private static string BuildProgram(TrussManifest manifest)
        {
            var name = manifest.Name;
            var program = new StringBuilder();

            program.AppendLine($"using {name}.Application;");

            if (manifest.UsesEntityFramework)
            {
                program.AppendLine($"using {name}.Infrastructure;");
                program.AppendLine("using Microsoft.EntityFrameworkCore;");
            }

            program.AppendLine();
            program.AppendLine("var builder = Host.CreateApplicationBuilder(args);");
            program.AppendLine();

            if (manifest.UsesEntityFramework)
            {
                program.AppendLine($"""
                    builder.Services.AddDbContext<AppDbContext>(options =>
                        options.{ProviderMethod(manifest.Database)}(builder.Configuration.GetConnectionString("Default")));
                    """);
                program.AppendLine();
            }

            program.AppendLine("""
                builder.Services.AddTruss(options =>
                {
                    options.AddAssembly<ApplicationAssemblyMarker>();
                });
                """);

            if (manifest.UsesEntityFramework)
            {
                program.AppendLine();
                program.AppendLine("builder.Services.AddTrussEntityFramework<AppDbContext>();");
            }

            program.AppendLine();
            program.AppendLine("""
                builder.Services.AddTrussMessaging(options =>
                {
                    options.AddAssembly<ApplicationAssemblyMarker>();
                });
                """);

            manifest.Settings.TryGetValue("messaging.transport", out var transport);
            program.AppendLine();
            program.AppendLine(ModuleInstaller.TransportRegistration(transport ?? "inmemory"));

            if (manifest.UsesEntityFramework)
            {
                program.AppendLine();
                program.AppendLine("builder.Services.AddTrussOutbox<AppDbContext>();");
            }

            if (manifest.Modules.Contains("jobs"))
            {
                program.AppendLine();
                program.AppendLine("""
                    builder.Services.AddTrussJobs(options =>
                    {
                        options.AddAssembly<ApplicationAssemblyMarker>();
                    });
                    """);

                if (manifest.UsesEntityFramework)
                {
                    program.AppendLine();
                    program.AppendLine("builder.Services.AddTrussJobsEntityFramework<AppDbContext>();");
                }
            }

            program.AppendLine();
            program.Append("builder.Build().Run();");

            return program.ToString();
        }

        private static string BuildAppSettings(TrussManifest manifest)
        {
            if (!manifest.UsesEntityFramework)
            {
                return """
                    {
                      "Logging": {
                        "LogLevel": {
                          "Default": "Information"
                        }
                      }
                    }
                    """;
            }

            return $$"""
                {
                  "ConnectionStrings": {
                    "Default": "{{ConnectionString(manifest)}}"
                  },
                  "Logging": {
                    "LogLevel": {
                      "Default": "Information",
                      "Microsoft.EntityFrameworkCore": "Warning"
                    }
                  }
                }
                """;
        }

        private static void AddToSolution(TrussManifest manifest, string root, string workerProject, Action<string> log)
        {
            var solution = Path.Combine(root, $"{manifest.Name}.slnx");
            var entry = $"""    <Project Path="src/{manifest.Name}.Worker/{manifest.Name}.Worker.csproj" />""";

            if (!SourceEditor.InsertBefore(solution, "  </Folder>", entry))
                log($"Could not update the solution automatically. Add to {manifest.Name}.slnx: {entry.Trim()}");
        }

        private static string ProviderMethod(string database) => database switch
        {
            "postgres" => "UseNpgsql",
            "sqlserver" => "UseSqlServer",
            _ => "UseSqlite"
        };

        private static string ConnectionString(TrussManifest manifest) => manifest.Database switch
        {
            "postgres" => $"Host=localhost;Port=5432;Database={manifest.Name.ToLowerInvariant()};Username=postgres;Password=truss",
            "sqlserver" => $"Server=localhost,1433;Database={manifest.Name.ToLowerInvariant()};User Id=sa;Password=Truss!Passw0rd;TrustServerCertificate=true",
            _ => $"Data Source={manifest.Name.ToLowerInvariant()}.db"
        };
    }
}
