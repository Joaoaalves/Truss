using Xunit;

namespace Truss.Cli.Tests
{
    public class DevPlannerTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        private (TrussManifest Manifest, string Root) Load(string name)
        {
            var root = _workspace.Root(name);
            return (TrussManifest.Load(root)!, root);
        }

        [Fact]
        public void PlainProject_WatchesTheApi_AndListsScalar()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var (manifest, root) = Load("Shop");

            var plan = DevPlanner.Build(manifest, root);

            Assert.False(plan.RunCompose);
            Assert.EndsWith(Path.Combine("src", "Shop.Api"), plan.ApiProjectPath);
            Assert.Contains(plan.Urls, url => url.Label == "API" && url.Url == "http://localhost:5000");
            Assert.Contains(plan.Urls, url => url.Label == "Scalar" && url.Url == "http://localhost:5000/scalar");
            Assert.DoesNotContain(plan.Urls, url => url.Label == "Dashboard");
        }

        [Fact]
        public void FullProject_RunsCompose_AndListsEveryUrl()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--docker"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "messaging", "--transport", "rabbitmq", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "jobs", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "observability", "--dashboard", "aspire", "--project", root));

            var (manifest, _) = Load("Shop");
            var plan = DevPlanner.Build(manifest, root);

            Assert.True(plan.RunCompose);
            Assert.Contains(plan.Urls, url => url.Label == "Dashboard" && url.Url == "http://localhost:18888");
            Assert.Contains(plan.Urls, url => url.Label == "RabbitMQ" && url.Url == "http://localhost:15672");
            Assert.Contains(plan.Urls, url => url.Label == "Jobs");
        }

        [Fact]
        public void ScaffoldedProgram_ServesOpenApiAndScalar()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddOpenApi", program);
            Assert.Contains("MapScalarApiReference", program);

            var apiCsproj = _workspace.ReadFile("Shop", "src", "Shop.Api", "Shop.Api.csproj");
            Assert.Contains("Scalar.AspNetCore", apiCsproj);
            Assert.Contains("Microsoft.AspNetCore.OpenApi", apiCsproj);
        }

        public void Dispose() => _workspace.Dispose();
    }
}
