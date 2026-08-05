namespace Truss.Cli
{
    internal static class ModuleInstaller
    {
        public static readonly string[] Modules = ["messaging", "jobs", "observability", "mapping", "auth"];

        public static readonly string[] Transports = ["inmemory", "postgres", "rabbitmq", "redis"];

        public static int Install(string module, string? transport, TrussManifest manifest, string root, Action<string> log)
        {
            if (!Modules.Contains(module))
            {
                log($"Unknown module '{module}'. Available modules: {string.Join(", ", Modules)}.");
                return 1;
            }

            if (manifest.Modules.Contains(module))
            {
                log($"The {module} module is already installed.");
                return 0;
            }

            var result = module switch
            {
                "messaging" => InstallMessaging(transport, manifest, root, log),
                "jobs" => InstallJobs(manifest, root, log),
                "mapping" => InstallMapping(manifest, root),
                "auth" => InstallAuth(transport, manifest, root, log),
                _ => InstallObservability(manifest, root, log)
            };

            if (result != 0)
                return result;

            manifest.Modules.Add(module);
            ComposeGenerator.Write(manifest, root);
            manifest.Save(root);

            log($"The {module} module was installed. Run truss doctor to verify the project.");
            return 0;
        }

        private static int InstallMessaging(string? transport, TrussManifest manifest, string root, Action<string> log)
        {
            transport ??= "inmemory";

            if (!Transports.Contains(transport))
            {
                log($"Unknown transport '{transport}'. Available transports: {string.Join(", ", Transports)}.");
                return 1;
            }

            var version = manifest.TrussVersion;
            var infrastructureHost = manifest.UsesEntityFramework ? manifest.InfrastructureProject : manifest.ApiProject;
            var hostCsproj = CsprojPath(root, infrastructureHost);

            CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApplicationProject), "Truss.Messaging.Abstractions", version);
            CsprojEditor.AddPackageReference(hostCsproj, "Truss.Messaging", version);

            if (transport == "postgres")
                CsprojEditor.AddPackageReference(hostCsproj, "Truss.Messaging.Postgres", version);
            else if (transport == "rabbitmq")
                CsprojEditor.AddPackageReference(hostCsproj, "Truss.Messaging.RabbitMq", version);
            else if (transport == "redis")
                CsprojEditor.AddPackageReference(hostCsproj, "Truss.Messaging.Redis", version);

            if (manifest.UsesEntityFramework)
                CsprojEditor.AddPackageReference(hostCsproj, "Truss.Messaging.EntityFrameworkCore", version);

            var registration = $$"""
                builder.Services.AddTrussMessaging(options =>
                {
                    options.AddAssembly<ApplicationAssemblyMarker>();
                });

                {{TransportRegistration(transport)}}
                """;

            if (manifest.UsesEntityFramework)
                registration += $"{Environment.NewLine}{Environment.NewLine}builder.Services.AddTrussOutbox<AppDbContext>();";

            InsertServices(root, manifest, registration, log);

            if (manifest.UsesEntityFramework)
                InsertModelConfiguration(root, manifest, "modelBuilder.ApplyTrussOutbox();", log);

            manifest.Settings["messaging.transport"] = transport;
            return 0;
        }

        private static string TransportRegistration(string transport) => transport switch
        {
            "postgres" => """
                builder.Services.AddTrussPostgresTransport(options =>
                {
                    options.ConnectionString = builder.Configuration.GetConnectionString("Default")!;
                });
                """,
            "rabbitmq" => """
                builder.Services.AddTrussRabbitMqTransport(options =>
                {
                    options.ConnectionString = builder.Configuration.GetConnectionString("RabbitMq") ?? "amqp://guest:guest@localhost:5672";
                });
                """,
            "redis" => """
                builder.Services.AddTrussRedisTransport(options =>
                {
                    options.ConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
                });
                """,
            _ => "builder.Services.AddTrussInMemoryTransport();"
        };

        private static int InstallJobs(TrussManifest manifest, string root, Action<string> log)
        {
            if (!manifest.Modules.Contains("messaging"))
            {
                log("The jobs module requires messaging. Run: truss add messaging");
                return 1;
            }

            var version = manifest.TrussVersion;
            var infrastructureHost = manifest.UsesEntityFramework ? manifest.InfrastructureProject : manifest.ApiProject;

            CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApplicationProject), "Truss.Jobs.Abstractions", version);
            CsprojEditor.AddPackageReference(CsprojPath(root, infrastructureHost), "Truss.Jobs", version);
            CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApiProject), "Truss.Jobs.AspNetCore", version);

            if (manifest.UsesEntityFramework)
                CsprojEditor.AddPackageReference(CsprojPath(root, infrastructureHost), "Truss.Jobs.EntityFrameworkCore", version);

            var registration = """
                builder.Services.AddTrussJobs(options =>
                {
                    options.AddAssembly<ApplicationAssemblyMarker>();
                });
                """;

            if (manifest.UsesEntityFramework)
                registration += $"{Environment.NewLine}{Environment.NewLine}builder.Services.AddTrussJobsEntityFramework<AppDbContext>();";

            InsertServices(root, manifest, registration, log);
            InsertEndpoint(root, manifest, "app.MapTrussJobs();", log);

            if (manifest.UsesEntityFramework)
                InsertModelConfiguration(root, manifest, "modelBuilder.ApplyTrussJobs();", log);

            return 0;
        }

        private static int InstallAuth(string? provider, TrussManifest manifest, string root, Action<string> log)
        {
            provider ??= "jwt";

            if (provider != "jwt")
            {
                log($"Unknown auth provider '{provider}'. Available now: jwt. Identity integration is on the roadmap.");
                return 1;
            }

            var result = AuthScaffolder.Install(manifest, root, log);

            if (result == 0)
            {
                log("The Accounts context was scaffolded into your projects. It is your code: edit the User entity and the commands freely.");
                log("A development signing key was written to appsettings.json; override it per environment with Truss__Auth__Jwt__SigningKey.");
            }

            return result;
        }

        private static int InstallMapping(TrussManifest manifest, string root)
        {
            CsprojEditor.AddPackageReference(
                CsprojPath(root, manifest.ApplicationProject),
                "Truss.Mapping",
                manifest.TrussVersion,
                developmentDependency: true);

            return 0;
        }

        private static int InstallObservability(TrussManifest manifest, string root, Action<string> log)
        {
            var version = manifest.TrussVersion;

            CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApiProject), "Truss.Observability", version);
            CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApiProject), "Truss.Observability.AspNetCore", version);

            InsertServices(root, manifest, "builder.Services.AddTrussObservability();", log);

            var programPath = ProgramPath(root, manifest);

            if (!SourceEditor.InsertAfter(programPath, "var app = builder.Build();", "app.UseTrussCorrelation();"))
                log("Could not update Program.cs automatically. Add after building the app: app.UseTrussCorrelation();");

            return 0;
        }

        private static void InsertServices(string root, TrussManifest manifest, string registration, Action<string> log)
        {
            if (!SourceEditor.InsertBefore(ProgramPath(root, manifest), "var app = builder.Build();", registration))
            {
                log("Could not update Program.cs automatically. Add before building the app:");
                log(registration);
            }
        }

        private static void InsertEndpoint(string root, TrussManifest manifest, string endpoint, Action<string> log)
        {
            if (!SourceEditor.InsertBefore(ProgramPath(root, manifest), "app.Run();", endpoint))
                log($"Could not update Program.cs automatically. Add before app.Run(): {endpoint}");
        }

        private static void InsertModelConfiguration(string root, TrussManifest manifest, string line, Action<string> log)
        {
            var contextPath = Path.Combine(root, manifest.InfrastructureProject, "AppDbContext.cs");
            var anchor = "modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);";

            if (!SourceEditor.InsertAfter(contextPath, anchor, $"            {line}"))
                log($"Could not update AppDbContext.cs automatically. Add to OnModelCreating: {line}");
        }

        private static string ProgramPath(string root, TrussManifest manifest)
        {
            return Path.Combine(root, manifest.ApiProject, "Program.cs");
        }

        private static string CsprojPath(string root, string projectDirectory)
        {
            var directory = Path.Combine(root, projectDirectory);
            return Directory.EnumerateFiles(directory, "*.csproj").First();
        }
    }
}
