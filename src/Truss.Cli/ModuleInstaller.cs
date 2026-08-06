namespace Truss.Cli
{
    internal static class ModuleInstaller
    {
        public static readonly string[] Modules = ["email", "messaging", "jobs", "observability", "mapping", "auth", "worker"];

        public static readonly string[] Transports = ["inmemory", "postgres", "rabbitmq", "redis"];

        public static readonly string[] Dashboards = ["aspire", "grafana", "seq"];

        public static int Install(string module, string? transport, TrussManifest manifest, string root, Action<string> log)
        {
            if (!Modules.Contains(module))
            {
                log($"Unknown module '{module}'. Available modules: {string.Join(", ", Modules)}.");
                return 1;
            }

            if (manifest.Modules.Contains(module))
            {
                if (module == "observability" && transport is not null)
                {
                    var dashboardResult = InstallDashboard(transport, manifest, root, log);

                    if (dashboardResult != 0)
                        return dashboardResult;

                    ComposeGenerator.Write(manifest, root);
                    AgentsGenerator.Write(manifest, root);
                    manifest.Save(root);

                    log($"The {transport} dashboard was wired. Run docker compose up to start it.");
                    return 0;
                }

                log($"The {module} module is already installed.");
                return 0;
            }

            var result = module switch
            {
                "messaging" => InstallMessaging(transport, manifest, root, log),
                "jobs" => InstallJobs(manifest, root, log),
                "mapping" => InstallMapping(manifest, root),
                "auth" => InstallAuth(transport, manifest, root, log),
                "worker" => WorkerScaffolder.Install(manifest, root, log),
                "email" => InstallEmail(transport, manifest, root, log),
                _ => InstallObservability(transport, manifest, root, log)
            };

            if (result != 0)
                return result;

            manifest.Modules.Add(module);
            ComposeGenerator.Write(manifest, root);
            AgentsGenerator.Write(manifest, root);
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

        internal static string TransportRegistration(string transport) => transport switch
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

            if (provider is not ("jwt" or "identity"))
            {
                log($"Unknown auth provider '{provider}'. Available providers: jwt, identity.");
                return 1;
            }

            var result = AuthScaffolder.Install(provider, manifest, root, log);

            if (result == 0)
            {
                log("The Accounts context was scaffolded into your projects. It is your code: edit the User entity and the commands freely.");
                log("A development signing key was written to appsettings.json; override it per environment with Truss__Auth__Jwt__SigningKey.");

                if (provider == "identity")
                    log("Credential mechanics run through ASP.NET Core Identity's UserManager; tune the password policy in AccountsModule.cs.");
            }

            return result;
        }


        private static int InstallEmail(string? provider, TrussManifest manifest, string root, Action<string> log)
        {
            provider ??= "console";

            if (provider is not ("console" or "smtp" or "resend"))
            {
                log($"Unknown email provider '{provider}'. Available providers: console, smtp, resend.");
                return 1;
            }

            var version = manifest.TrussVersion;

            CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApplicationProject), "Truss.Email.Abstractions", version);
            CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApiProject), "Truss.Email", version);

            if (provider == "resend")
                CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApiProject), "Truss.Email.Resend", version);

            InsertServices(root, manifest, EmailRegistration(provider) + Environment.NewLine + "builder.Services.AddTrussEmailValidation(options => builder.Configuration.GetSection(\"Truss:Email:Validation\").Bind(options));", log);
            manifest.Settings["email.provider"] = provider;

            if (provider == "smtp")
            {
                AppSettingsEditor.SetSection(root, manifest, "Truss:Email:Smtp", new Dictionary<string, object>
                {
                    ["Host"] = "localhost",
                    ["Port"] = 1025,
                    ["From"] = $"noreply@{manifest.Name.ToLowerInvariant()}.dev",
                    ["UseStartTls"] = false
                }, log);

                log("Development SMTP points at Mailpit (docker compose up starts it; UI at http://localhost:8025).");
                log("Override per environment with Truss__Email__Smtp__* variables.");
            }
            else if (provider == "resend")
            {
                AppSettingsEditor.SetSection(root, manifest, "Truss:Email:Resend", new Dictionary<string, object>
                {
                    ["From"] = $"noreply@{manifest.Name.ToLowerInvariant()}.dev"
                }, log);

                log("Set the API key per environment with Truss__Email__Resend__ApiKey; it never goes into appsettings.json.");
                log("The From domain must be verified in the Resend dashboard.");
            }
            else
            {
                log("Emails print to the console log in this mode; switch later with truss add email --provider smtp or --provider resend.");
            }

            return 0;
        }

        internal static string EmailRegistration(string provider) => provider switch
        {
            "smtp" => "builder.Services.AddTrussSmtpEmail(options => builder.Configuration.GetSection(\"Truss:Email:Smtp\").Bind(options));",
            "resend" => "builder.Services.AddTrussResendEmail(options => builder.Configuration.GetSection(\"Truss:Email:Resend\").Bind(options));",
            _ => "builder.Services.AddTrussConsoleEmail();"
        };

        private static int InstallMapping(TrussManifest manifest, string root)
        {
            CsprojEditor.AddPackageReference(
                CsprojPath(root, manifest.ApplicationProject),
                "Truss.Mapping",
                manifest.TrussVersion,
                developmentDependency: true);

            return 0;
        }

        private static int InstallObservability(string? dashboard, TrussManifest manifest, string root, Action<string> log)
        {
            var version = manifest.TrussVersion;

            CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApiProject), "Truss.Observability", version);
            CsprojEditor.AddPackageReference(CsprojPath(root, manifest.ApiProject), "Truss.Observability.AspNetCore", version);

            InsertServices(root, manifest, "builder.Services.AddTrussObservability();", log);

            var programPath = ProgramPath(root, manifest);

            if (!SourceEditor.InsertAfter(programPath, "var app = builder.Build();", "app.UseTrussCorrelation();"))
                log("Could not update Program.cs automatically. Add after building the app: app.UseTrussCorrelation();");

            if (dashboard is not null)
                return InstallDashboard(dashboard, manifest, root, log);

            return 0;
        }

        private static int InstallDashboard(string dashboard, TrussManifest manifest, string root, Action<string> log)
        {
            if (!Dashboards.Contains(dashboard))
            {
                log($"Unknown dashboard '{dashboard}'. Available dashboards: {string.Join(", ", Dashboards)}.");
                return 1;
            }

            CsprojEditor.AddPackageReference(
                CsprojPath(root, manifest.ApiProject), "Truss.Observability.OpenTelemetry", manifest.TrussVersion);

            InsertServices(root, manifest, "builder.Services.AddTrussOpenTelemetry();", log);
            LaunchSettingsEditor.SetEnvironmentVariables(root, manifest, OtlpEnvironment(dashboard), log);

            manifest.Settings["observability.dashboard"] = dashboard;

            if (!manifest.Docker)
                log("The project has no docker-compose.yml; start the dashboard yourself and point OTEL_EXPORTER_OTLP_ENDPOINT at it.");

            return 0;
        }

        private static Dictionary<string, string> OtlpEnvironment(string dashboard) => dashboard switch
        {
            "seq" => new Dictionary<string, string>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:5341/ingest/otlp",
                ["OTEL_EXPORTER_OTLP_PROTOCOL"] = "http/protobuf"
            },
            _ => new Dictionary<string, string>
            {
                ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317"
            }
        };

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
