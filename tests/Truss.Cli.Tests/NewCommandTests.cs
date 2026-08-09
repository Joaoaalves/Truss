using Xunit;

namespace Truss.Cli.Tests
{
    public class NewCommandTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        [Fact]
        public void New_WithSample_ScaffoldsFullSolutionWithTheCatalog()
        {
            var exitCode = _workspace.Scaffold("Shop", "postgres", "--docker", "--sample");

            Assert.Equal(0, exitCode);
            Assert.True(_workspace.FileExists("Shop", "truss.json"));
            Assert.True(_workspace.FileExists("Shop", "Shop.slnx"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Shop.Domain.csproj"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "ApplicationAssemblyMarker.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Api", "Program.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Api", "Properties", "launchSettings.json"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Catalog", "Product", "Product.cs"));
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
        public void New_ByDefault_ScaffoldsClean_WithTestProjects()
        {
            var exitCode = _workspace.Scaffold("Shop", "postgres");

            Assert.Equal(0, exitCode);
            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Domain", "Catalog", "Product", "Product.cs"));
            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "InfrastructureModule.cs"));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.DoesNotContain("CreateProduct", program);
            Assert.DoesNotContain("AddInfrastructure", program);

            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.Domain.Tests", "Shop.Domain.Tests.csproj"));
            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.IntegrationTests", "Shop.IntegrationTests.csproj"));

            var smoke = _workspace.ReadFile("Shop", "tests", "Shop.IntegrationTests", "HostSmokeTests.cs");
            Assert.Contains("TrussTestHost.Start<AppDbContext>", smoke);

            var solution = _workspace.ReadFile("Shop", "Shop.slnx");
            Assert.Contains("tests/Shop.Domain.Tests/Shop.Domain.Tests.csproj", solution);

            var manifest = TrussManifest.Load(_workspace.Root("Shop"));
            Assert.False(manifest!.Sample);
            Assert.True(manifest.Tests);
        }

        [Fact]
        public void New_WithNoTests_SkipsTestProjects_AndAddTestsBringsThemLater()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--no-tests"));
            var root = _workspace.Root("Shop");

            Assert.False(Directory.Exists(Path.Combine(root, "tests")));
            Assert.False(TrussManifest.Load(root)!.Tests);

            Assert.Equal(0, _workspace.Run("add", "tests", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.Domain.Tests", "Shop.Domain.Tests.csproj"));
            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.IntegrationTests", "HostSmokeTests.cs"));
            Assert.Contains("tests/Shop.IntegrationTests/Shop.IntegrationTests.csproj", _workspace.ReadFile("Shop", "Shop.slnx"));
            Assert.True(TrussManifest.Load(root)!.Tests);

            Assert.Equal(0, _workspace.Run("doctor", "--project", root));
        }

        [Fact]
        public void New_WithSampleAndTests_ScaffoldsTheSampleTests()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--sample"));

            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.Domain.Tests", "Catalog", "ProductTests.cs"));

            var integration = _workspace.ReadFile("Shop", "tests", "Shop.IntegrationTests", "Catalog", "CatalogTests.cs");
            Assert.Contains("AddScoped<IProductRepository, EfProductRepository>", integration);
        }

        [Fact]
        public void New_WithoutDatabase_SkipsInfrastructureAndSample()
        {
            var exitCode = _workspace.Scaffold("Tool", "none");

            Assert.Equal(0, exitCode);
            Assert.False(Directory.Exists(Path.Combine(_workspace.Root("Tool"), "src", "Tool.Infrastructure")));
            Assert.False(_workspace.FileExists("Tool", "src", "Tool.Domain", "Catalog", "Product", "Product.cs"));

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
