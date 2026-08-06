using Xunit;

namespace Truss.Cli.Tests
{
    public class AddCommandTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        private string ScaffoldShop()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            return _workspace.Root("Shop");
        }

        [Fact]
        public void AddMessaging_WiresPackagesProgramAndManifest()
        {
            var root = ScaffoldShop();

            var exitCode = _workspace.Run("add", "messaging", "--transport", "redis", "--project", root);

            Assert.Equal(0, exitCode);

            var applicationCsproj = _workspace.ReadFile("Shop", "src", "Shop.Application", "Shop.Application.csproj");
            Assert.Contains("Truss.Messaging.Abstractions", applicationCsproj);

            var infrastructureCsproj = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Shop.Infrastructure.csproj");
            Assert.Contains("Truss.Messaging.Redis", infrastructureCsproj);
            Assert.Contains("Truss.Messaging.EntityFrameworkCore", infrastructureCsproj);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussMessaging", program);
            Assert.Contains("AddTrussRedisTransport", program);
            Assert.Contains("AddTrussOutbox<AppDbContext>", program);

            var dbContext = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.Contains("ApplyTrussOutbox", dbContext);

            var manifest = TrussManifest.Load(root);
            Assert.Contains("messaging", manifest!.Modules);
            Assert.Equal("redis", manifest.Settings["messaging.transport"]);
        }

        [Fact]
        public void AddMessaging_WithRabbitMq_WiresTransportAndCompose()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--docker"));
            var root = _workspace.Root("Shop");

            var exitCode = _workspace.Run("add", "messaging", "--transport", "rabbitmq", "--project", root);

            Assert.Equal(0, exitCode);

            var infrastructureCsproj = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Shop.Infrastructure.csproj");
            Assert.Contains("Truss.Messaging.RabbitMq", infrastructureCsproj);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussRabbitMqTransport", program);

            var compose = _workspace.ReadFile("Shop", "docker-compose.yml");
            Assert.Contains("rabbitmq:4-management-alpine", compose);
            Assert.Contains("rabbitmq-data:", compose);

            var manifest = TrussManifest.Load(root);
            Assert.Equal("rabbitmq", manifest!.Settings["messaging.transport"]);
        }

        [Fact]
        public void AddJobs_RequiresMessaging()
        {
            var root = ScaffoldShop();

            Assert.Equal(1, _workspace.Run("add", "jobs", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "jobs", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussJobs", program);
            Assert.Contains("app.MapTrussJobs();", program);
        }

        [Fact]
        public void AddObservability_WiresProgram()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "observability", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussObservability", program);
            Assert.Contains("app.UseTrussCorrelation();", program);
        }

        [Fact]
        public void AddObservability_WithDashboard_WiresOpenTelemetryAndCompose()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--docker"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "observability", "--dashboard", "aspire", "--project", root));

            var apiCsproj = _workspace.ReadFile("Shop", "src", "Shop.Api", "Shop.Api.csproj");
            Assert.Contains("Truss.Observability.OpenTelemetry", apiCsproj);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussOpenTelemetry", program);

            var compose = _workspace.ReadFile("Shop", "docker-compose.yml");
            Assert.Contains("aspire-dashboard", compose);

            var launchSettings = _workspace.ReadFile("Shop", "src", "Shop.Api", "Properties", "launchSettings.json");
            Assert.Contains("OTEL_EXPORTER_OTLP_ENDPOINT", launchSettings);

            var manifest = TrussManifest.Load(root);
            Assert.Equal("aspire", manifest!.Settings["observability.dashboard"]);
        }

        [Fact]
        public void AddDashboard_ToObservabilityInstalledEarlier_Works()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--docker"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "observability", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "observability", "--dashboard", "seq", "--project", root));

            var compose = _workspace.ReadFile("Shop", "docker-compose.yml");
            Assert.Contains("datalust/seq", compose);
            Assert.Contains("seq-data:", compose);

            var launchSettings = _workspace.ReadFile("Shop", "src", "Shop.Api", "Properties", "launchSettings.json");
            Assert.Contains("http/protobuf", launchSettings);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            var occurrences = program.Split("AddTrussOpenTelemetry").Length - 1;
            Assert.Equal(1, occurrences);
        }

        [Fact]
        public void AddMapping_AddsDevelopmentDependency()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "mapping", "--project", root));

            var applicationCsproj = _workspace.ReadFile("Shop", "src", "Shop.Application", "Shop.Application.csproj");
            Assert.Contains("Include=\"Truss.Mapping\"", applicationCsproj);
            Assert.Contains("PrivateAssets=\"all\"", applicationCsproj);

            var manifest = TrussManifest.Load(root);
            Assert.Contains("mapping", manifest!.Modules);
        }

        [Fact]
        public void AddAuth_ScaffoldsEditableAccountsContext()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "auth", "--provider", "jwt", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Accounts", "User.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "LoginHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "IUserCredentialsStore.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "EfUserRepository.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "EfUserCredentialsStore.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "EfRefreshTokenStore.cs"));

            var user = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Accounts", "User.cs");
            Assert.Contains("namespace Shop.Domain.Accounts", user);
            Assert.Contains("public class User : AggregateRoot<UserId>", user);
            Assert.DoesNotContain("PasswordHash", user);
            Assert.DoesNotContain("hash", user, StringComparison.OrdinalIgnoreCase);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussJwtAuth", program);
            Assert.Contains("app.UseAuthentication();", program);
            Assert.Contains("/auth/login", program);

            var appsettings = _workspace.ReadFile("Shop", "src", "Shop.Api", "appsettings.json");
            Assert.Contains("SigningKey", appsettings);

            var applicationCsproj = _workspace.ReadFile("Shop", "src", "Shop.Application", "Shop.Application.csproj");
            Assert.Contains("Truss.Auth.Abstractions", applicationCsproj);

            var manifest = TrussManifest.Load(root);
            Assert.Contains("auth", manifest!.Modules);
            Assert.Equal("jwt", manifest.Settings["auth.provider"]);
        }

        [Fact]
        public void AddAuth_WithIdentityProvider_ScaffoldsIdentityBackedStores()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "auth", "--provider", "identity", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "ApplicationUser.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "IdentityModelConfiguration.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "IdentityUserCredentialsStore.cs"));
            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "UserCredential.cs"));

            var user = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Accounts", "User.cs");
            Assert.DoesNotContain("hash", user, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Identity", user);

            var infrastructureCsproj = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Shop.Infrastructure.csproj");
            Assert.Contains("Microsoft.AspNetCore.Identity.EntityFrameworkCore", infrastructureCsproj);

            var accountsModule = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AccountsModule.cs");
            Assert.Contains("AddIdentityCore<ApplicationUser>", accountsModule);
            Assert.Contains("IdentityUserCredentialsStore", accountsModule);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussJwtAuth", program);

            var manifest = TrussManifest.Load(root);
            Assert.Equal("identity", manifest!.Settings["auth.provider"]);
        }

        [Fact]
        public void AddAuth_RequiresDatabase()
        {
            Assert.Equal(0, _workspace.Scaffold("Tool", "none"));

            Assert.Equal(1, _workspace.Run("add", "auth", "--project", _workspace.Root("Tool")));
        }

        [Fact]
        public void AddEmail_PrintsToTheConsoleByDefault()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "email", "--project", root));

            var applicationCsproj = _workspace.ReadFile("Shop", "src", "Shop.Application", "Shop.Application.csproj");
            Assert.Contains("Truss.Email.Abstractions", applicationCsproj);

            var apiCsproj = _workspace.ReadFile("Shop", "src", "Shop.Api", "Shop.Api.csproj");
            Assert.Contains("Include=\"Truss.Email\"", apiCsproj);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussConsoleEmail", program);

            var manifest = TrussManifest.Load(root);
            Assert.Equal("console", manifest!.Settings["email.provider"]);
        }

        [Fact]
        public void AddEmail_Smtp_WiresMailpitForDevelopment()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--docker"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "email", "--provider", "smtp", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussSmtpEmail", program);

            var appsettings = _workspace.ReadFile("Shop", "src", "Shop.Api", "appsettings.json");
            Assert.Contains("\"Smtp\"", appsettings);
            Assert.Contains("1025", appsettings);

            var compose = _workspace.ReadFile("Shop", "docker-compose.yml");
            Assert.Contains("axllent/mailpit", compose);

            var manifest = TrussManifest.Load(root);
            var plan = DevPlanner.Build(manifest!, root);
            Assert.Contains(plan.Urls, url => url.Label == "Mailpit" && url.Url == "http://localhost:8025");
        }

        [Fact]
        public void AddWorker_ScaffoldsAConsumerProcess()
        {
            var root = ScaffoldShop();

            Assert.Equal(1, _workspace.Run("add", "worker", "--project", root));

            Assert.Equal(0, _workspace.Run("add", "messaging", "--transport", "redis", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "jobs", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));

            var csproj = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Shop.Worker.csproj");
            Assert.Contains("Microsoft.NET.Sdk.Worker", csproj);
            Assert.Contains("Shop.Infrastructure.csproj", csproj);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Program.cs");
            Assert.Contains("Host.CreateApplicationBuilder", program);
            Assert.Contains("AddTrussMessaging", program);
            Assert.Contains("AddTrussRedisTransport", program);
            Assert.Contains("AddTrussOutbox<AppDbContext>", program);
            Assert.Contains("AddTrussJobs", program);
            Assert.DoesNotContain("WebApplication", program);

            var solution = _workspace.ReadFile("Shop", "Shop.slnx");
            Assert.Contains("Shop.Worker.csproj", solution);

            var manifest = TrussManifest.Load(root);
            Assert.Contains("worker", manifest!.Modules);

            Assert.Equal(0, _workspace.Run("doctor", "--project", root));
        }

        [Fact]
        public void AddSameModuleTwice_IsIdempotent()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            var occurrences = program.Split("AddTrussMessaging").Length - 1;
            Assert.Equal(1, occurrences);
        }

        public void Dispose() => _workspace.Dispose();
    }
}
