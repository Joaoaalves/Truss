using Xunit;

namespace Truss.Cli.Tests
{
    public class NewCommandTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        [Fact]
        public void New_WithDatabase_ScaffoldsFullSolution()
        {
            var exitCode = _workspace.Scaffold("Shop", "postgres", "--docker");

            Assert.Equal(0, exitCode);
            Assert.True(_workspace.FileExists("Shop", "truss.json"));
            Assert.True(_workspace.FileExists("Shop", "Shop.slnx"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Shop.Domain.csproj"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "ApplicationAssemblyMarker.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Api", "Program.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Api", "Properties", "launchSettings.json"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Catalog", "Product.cs"));
            Assert.True(_workspace.FileExists("Shop", "docker-compose.yml"));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("UseNpgsql", program);
            Assert.Contains("app.MapCommand<CreateProduct, Guid>", program);
            Assert.Contains("RunTrussSeeders", program);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Catalog", "CatalogSeeder.cs"));

            var compose = _workspace.ReadFile("Shop", "docker-compose.yml");
            Assert.Contains("postgres:16-alpine", compose);

            var manifest = TrussManifest.Load(_workspace.Root("Shop"));
            Assert.NotNull(manifest);
            Assert.Equal("postgres", manifest.Database);
            Assert.True(manifest.Sample);
        }

        [Fact]
        public void New_WithoutDatabase_SkipsInfrastructureAndSample()
        {
            var exitCode = _workspace.Scaffold("Tool", "none");

            Assert.Equal(0, exitCode);
            Assert.False(Directory.Exists(Path.Combine(_workspace.Root("Tool"), "src", "Tool.Infrastructure")));
            Assert.False(_workspace.FileExists("Tool", "src", "Tool.Domain", "Catalog", "Product.cs"));

            var program = _workspace.ReadFile("Tool", "src", "Tool.Api", "Program.cs");
            Assert.DoesNotContain("AddDbContext", program);
            Assert.DoesNotContain("AddTrussEntityFramework", program);
        }

        [Fact]
        public void New_WithInvalidName_Fails()
        {
            var exitCode = _workspace.Scaffold("1Bad", "sqlite");

            Assert.Equal(1, exitCode);
        }

        [Fact]
        public void New_OnExistingDirectory_Fails()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            Assert.Equal(1, _workspace.Scaffold("Shop", "sqlite"));
        }

        public void Dispose() => _workspace.Dispose();
    }
}
