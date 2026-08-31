using Xunit;

namespace Truss.Cli.Tests
{
    public class AddCommandTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        private string ScaffoldShop()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--sample"));
            return _workspace.Root("Shop");
        }

        [Fact]
        public void AddMessaging_WiresPackagesProgramAndManifest()
        {
            var root = ScaffoldShop();

            var exitCode = _workspace.Run("add", "messaging", "--transport", "redis", "--project", root);

            Assert.Equal(0, exitCode);

            var applicationCsproj = _workspace.ReadFile("Shop", "src", "Shop.Application", "Shop.Application.csproj");
            Assert.Contains("Truss.Messaging", applicationCsproj);

            var infrastructureCsproj = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Shop.Infrastructure.csproj");
            Assert.Contains("Truss.Messaging.Redis", infrastructureCsproj);
            Assert.Contains("Truss.EntityFrameworkCore", infrastructureCsproj);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussMessaging", program);
            Assert.Contains("AddTrussRedisTransport", program);
            Assert.Contains("AddTrussOutbox<AppDbContext>", program);
            Assert.Contains("AddTrussInbox<AppDbContext>", program);
            Assert.Contains("app.MapTrussOutbox();", program);

            var apiCsproj = _workspace.ReadFile("Shop", "src", "Shop.Api", "Shop.Api.csproj");
            Assert.Contains("Truss.AspNetCore", apiCsproj);

            var dbContext = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.Contains("ApplyTrussOutbox", dbContext);
            Assert.Contains("ApplyTrussInbox", dbContext);

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
        public void AddIdempotency_WiresAllThreeRegistrations()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "idempotency", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("builder.Services.AddTrussIdempotency<AppDbContext>();", program);
            Assert.Contains("app.UseTrussIdempotency();", program);

            var dbContext = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.Contains("modelBuilder.ApplyTrussIdempotency();", dbContext);

            var manifest = TrussManifest.Load(root);
            Assert.Contains("idempotency", manifest!.Modules);
        }

        [Fact]
        public void AddAuth_AfterTheWorkerExists_WiresTheAccountsIntoTheWorker()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));

            AssertWorkerRunsTheAccountSlice();
        }

        [Fact]
        public void AddWorker_AfterAuthExists_WiresTheAccountsIntoTheWorker()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));

            AssertWorkerRunsTheAccountSlice();
        }

        private void AssertWorkerRunsTheAccountSlice()
        {
            var program = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Program.cs");
            Assert.Contains("builder.Services.AddAccountsInfrastructure();", program);
            Assert.Contains("AddScoped<ICurrentUser, WorkerCurrentUser>();", program);
            Assert.Contains("AddTrussJwtTokens(", program);
            Assert.DoesNotContain("AddTrussJwtAuth(", program);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Worker", "WorkerCurrentUser.cs"));

            var csproj = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Shop.Worker.csproj");
            Assert.Contains("Truss.Auth.Jwt", csproj);

            // The same issuer, audience and key as the API, or tokens issued
            // by one host would not verify in the other.
            var apiSettings = _workspace.ReadFile("Shop", "src", "Shop.Api", "appsettings.json");
            var workerSettings = _workspace.ReadFile("Shop", "src", "Shop.Worker", "appsettings.json");
            var key = System.Text.Json.Nodes.JsonNode.Parse(apiSettings)!["Truss"]!["Auth"]!["Jwt"]!["SigningKey"]!.GetValue<string>();
            Assert.Contains(key, workerSettings);
        }


        [Fact]
        public void AddSupport_ScaffoldsTheContext_AndWiresBothHosts()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "support", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Support", "Ticket", "Ticket.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Support", "ITicketRepository.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Support", "EfTicketRepository.cs"));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("builder.Services.AddSupportInfrastructure();", program);
            Assert.Contains("app.MapCommand<OpenTicket, Guid>(\"/support/tickets\"", program);

            // Without rbac the staff surface still demands a signed-in caller.
            Assert.Contains("app.MapQuery<ListSupportQueue, PageResult<TicketSummaryDto>>(\"/support/queue\").RequireAuthorization();", program);

            var worker = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Program.cs");
            Assert.Contains("builder.Services.AddSupportInfrastructure();", worker);

            var manifest = TrussManifest.Load(root);
            Assert.Contains("support", manifest!.Modules);
            Assert.Equal("standalone", manifest.Settings["support.mode"]);
        }

        [Fact]
        public void AddSupport_WithRbac_PutsThePermissionOnTheStaffRoutes()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "rbac", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "support", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains(".RequirePermission(\"support.manage\");", program);
            Assert.Contains("options.AddRole(\"support\", \"support.manage\");", program);
        }

        [Fact]
        public void AddSupport_WithoutAuth_FailsWithGuidance()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(1, _workspace.Run("add", "support", "--project", root));
        }


        [Fact]
        public void AddSupport_DeckMode_WiresTheThinSurface_InBothHosts()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "support", "--deck", "http://deck.local:8080", "--project", root));

            // No local domain in deck mode: attendance happens on the deck.
            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Domain", "Support", "Ticket", "Ticket.cs"));

            var handler = _workspace.ReadFile("Shop", "src", "Shop.Application", "Support", "OpenTicket", "OpenTicketHandler.cs");
            Assert.Contains("ISupportDeckClient", handler);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussSupportDeck(", program);
            Assert.Contains("app.MapCommand<OpenTicket, Guid>(\"/support/tickets\"", program);
            Assert.Contains("/support/deck-webhook", program);
            Assert.DoesNotContain("/support/queue", program);

            // Without email there is no channel; the handler logs and explains.
            var notification = _workspace.ReadFile("Shop", "src", "Shop.Application", "Support", "SupportNotificationHandler.cs");
            Assert.Contains("ILogger<SupportNotificationHandler>", notification);

            var settings = _workspace.ReadFile("Shop", "src", "Shop.Api", "appsettings.json");
            Assert.Contains("http://deck.local:8080", settings);
            Assert.DoesNotContain("ApiKey", settings);

            var worker = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Program.cs");
            Assert.Contains("AddTrussSupportDeck(", worker);

            var applicationCsproj = _workspace.ReadFile("Shop", "src", "Shop.Application", "Shop.Application.csproj");
            Assert.Contains("Truss.Support", applicationCsproj);

            var manifest = TrussManifest.Load(root);
            Assert.Equal("deck", manifest!.Settings["support.mode"]);
        }

        [Fact]
        public void AddWorker_AfterDeckModeSupport_WiresTheClientIntoTheWorker()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "support", "--deck", "http://deck.local:8080", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));

            var worker = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Program.cs");
            Assert.Contains("AddTrussSupportDeck(", worker);
            Assert.Contains("ISupportRequesterSource", worker);
        }


        [Fact]
        public void AddDocker_Late_TurnsTheFlagAndWritesTheCompose()
        {
            // Postgres gives the compose a service to hold; sqlite composes
            // nothing and the generator rightly stays quiet.
            Assert.Equal(0, _workspace.Scaffold("Shop", "postgres"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "docker", "--project", root));

            var manifest = TrussManifest.Load(root);
            Assert.True(manifest!.Docker);
            Assert.True(_workspace.FileExists("Shop", "docker-compose.yml"));
        }

        [Fact]
        public void AddModule_AfterTheWorkerExists_SyncsItsProgram()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "jobs", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "email", "--provider", "smtp", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Program.cs");
            Assert.Contains("AddTrussJobs", program);
            Assert.Contains("AddTrussJobsEntityFramework<AppDbContext>", program);
            Assert.Contains("AddTrussSmtpEmail", program);
            Assert.Contains("AddTrussEmailValidation", program);

            var csproj = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Shop.Worker.csproj");
            Assert.Contains("Truss.Email", csproj);

            var settings = _workspace.ReadFile("Shop", "src", "Shop.Worker", "appsettings.json");
            Assert.Contains("\"Smtp\"", settings);
        }

        [Fact]
        public void AddAuth_ScaffoldsEditableAccountsContext()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "auth", "--provider", "jwt", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Accounts", "User", "User.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Accounts", "User", "ValueObjects", "UserId.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Accounts", "User", "Events", "UserRegistered.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Accounts", "User", "Rules", "EmailMustBeUnique.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "Login", "LoginHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "Login", "LoginValidator.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "DTOs", "AuthTokensDto.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "Rules", "InvalidCredentials.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "IUserCredentialsStore.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "EfUserRepository.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "EfUserCredentialsStore.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "EfRefreshTokenStore.cs"));

            var user = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Accounts", "User", "User.cs");
            Assert.Contains("namespace Shop.Domain.Accounts.User", user);
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
            Assert.Contains("Truss.Application", applicationCsproj);

            var manifest = TrussManifest.Load(root);
            Assert.Contains("auth", manifest!.Modules);
            Assert.Equal("jwt", manifest.Settings["auth.provider"]);
        }

        [Fact]
        public void AddAuth_WithEmailModule_ScaffoldsAccountFlows()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "email", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "IAccountSecurityStore.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "RequestPasswordReset", "RequestPasswordResetHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "VerifyTwoFactor", "VerifyTwoFactorHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "DTOs", "LoginResult.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "AccountTokenRecord.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "EfAccountSecurityStore.cs"));

            var user = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Accounts", "User", "User.cs");
            Assert.DoesNotContain("EmailConfirmed", user);
            Assert.DoesNotContain("TwoFactorEnabled", user);
            Assert.DoesNotContain("hash", user, StringComparison.OrdinalIgnoreCase);

            var credential = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Accounts", "UserCredential.cs");
            Assert.Contains("EmailConfirmed", credential);
            Assert.Contains("TwoFactorEnabled", credential);

            var accountsModule = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AccountsModule.cs");
            Assert.Contains("EfAccountTokenStore", accountsModule);
            Assert.Contains("EfAccountSecurityStore", accountsModule);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("MapCommand<Login, LoginResult>", program);
            Assert.Contains("/auth/password/request-reset", program);
            Assert.Contains("/auth/login/2fa", program);
            Assert.Contains("/auth/confirm-email", program);
        }

        [Fact]
        public void AddAuthFlows_RetrofitsAFlowlessInstallation()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(1, _workspace.Run("add", "auth", "--flows", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "email", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--flows", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "VerifyTwoFactor", "VerifyTwoFactorHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "ResetPassword", "ResetPasswordHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "DTOs", "LoginResult.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "EfAccountTokenStore.cs"));

            var login = _workspace.ReadFile("Shop", "src", "Shop.Application", "Accounts", "Login", "Login.cs");
            Assert.Contains("LoginResult", login);

            var accountsModule = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AccountsModule.cs");
            Assert.Contains("IAccountTokenStore", accountsModule);
            Assert.Contains("EfAccountSecurityStore", accountsModule);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("MapCommand<Login, LoginResult>", program);
            Assert.DoesNotContain("MapCommand<Login, AuthTokensDto>", program);
            Assert.Contains("/auth/password/request-reset", program);
            Assert.Contains("using Shop.Application.Accounts.VerifyTwoFactor;", program);

            var tests = _workspace.ReadFile("Shop", "tests", "Shop.IntegrationTests", "Accounts", "AccountsTests.cs");
            Assert.Contains("AddTrussConsoleEmail", tests);

            var manifest = TrussManifest.Load(root);
            Assert.Equal("true", manifest!.Settings["auth.flows"]);

            Assert.Equal(0, _workspace.Run("add", "auth", "--flows", "--project", root));
        }

        [Fact]
        public void AddAuthFlows_LeavesAnEditedLoginPairAlone()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "email", "--project", root));

            var handlerPath = Path.Combine(root, "src", "Shop.Application", "Accounts", "Login", "LoginHandler.cs");
            File.AppendAllText(handlerPath, "// custom lockout policy" + Environment.NewLine);

            var output = _workspace.Capture("add", "auth", "--flows", "--project", root);

            Assert.Contains("left alone", output);

            var login = _workspace.ReadFile("Shop", "src", "Shop.Application", "Accounts", "Login", "Login.cs");
            Assert.Contains("AuthTokensDto", login);

            var handler = _workspace.ReadFile("Shop", "src", "Shop.Application", "Accounts", "Login", "LoginHandler.cs");
            Assert.Contains("custom lockout policy", handler);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("MapCommand<Login, AuthTokensDto>", program);
            Assert.Contains("/auth/password/request-reset", program);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "VerifyTwoFactor", "VerifyTwoFactorHandler.cs"));
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

            var user = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Accounts", "User", "User.cs");
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
            Assert.Contains("Truss.Email", applicationCsproj);

            var apiCsproj = _workspace.ReadFile("Shop", "src", "Shop.Api", "Shop.Api.csproj");
            Assert.Contains("Include=\"Truss.Email\"", apiCsproj);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussConsoleEmail", program);
            Assert.Contains("AddTrussEmailValidation", program);

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
        public void AddEmail_Resend_WiresTheApiSender()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "email", "--provider", "resend", "--project", root));

            var apiCsproj = _workspace.ReadFile("Shop", "src", "Shop.Api", "Shop.Api.csproj");
            Assert.Contains("Truss.Email.Resend", apiCsproj);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussResendEmail", program);
            Assert.Contains("AddTrussEmailValidation", program);

            var appsettings = _workspace.ReadFile("Shop", "src", "Shop.Api", "appsettings.json");
            Assert.Contains("\"Resend\"", appsettings);
            Assert.DoesNotContain("ApiKey", appsettings);

            var manifest = TrussManifest.Load(root);
            Assert.Equal("resend", manifest!.Settings["email.provider"]);

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));

            var workerCsproj = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Shop.Worker.csproj");
            Assert.Contains("Truss.Email.Resend", workerCsproj);

            var workerProgram = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Program.cs");
            Assert.Contains("AddTrussResendEmail", workerProgram);
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
        public void AddTenancy_WiresIsolationWithoutTouchingTheDomain()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "tenancy", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussTenancy<AppDbContext>", program);
            Assert.Contains("app.UseTrussTenancy();", program);

            var dbContext = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.Contains("ApplyTrussTenancy(this)", dbContext);

            var product = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Catalog", "Product", "Product.cs");
            Assert.DoesNotContain("Tenant", product);

            var manifest = TrussManifest.Load(root);
            Assert.Contains("tenancy", manifest!.Modules);

            Assert.Equal(0, _workspace.Run("doctor", "--project", root));
        }

        [Fact]
        public void AddRbac_WiresRolesAndPermissions()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "rbac", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussRbac", program);
            Assert.Contains("AddTrussRbacEntityFramework<AppDbContext>", program);

            var dbContext = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.Contains("ApplyTrussRbac()", dbContext);

            var manifest = TrussManifest.Load(root);
            Assert.Contains("rbac", manifest!.Modules);

            Assert.Equal(0, _workspace.Run("doctor", "--project", root));
        }

        [Fact]
        public void TenancyAndRbac_AreIndependent()
        {
            Assert.Equal(0, _workspace.Scaffold("Solo", "sqlite"));
            var root = _workspace.Root("Solo");

            Assert.Equal(0, _workspace.Run("add", "rbac", "--project", root));

            var program = _workspace.ReadFile("Solo", "src", "Solo.Api", "Program.cs");
            Assert.Contains("AddTrussRbac", program);
            Assert.DoesNotContain("AddTrussTenancy", program);

            var apiCsproj = _workspace.ReadFile("Solo", "src", "Solo.Api", "Solo.Api.csproj");
            Assert.DoesNotContain("Truss.Tenancy", apiCsproj);
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
