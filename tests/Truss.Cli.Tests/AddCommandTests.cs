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
