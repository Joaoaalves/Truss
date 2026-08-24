using Xunit;

namespace Truss.Cli.Tests
{
    /// <summary>
    /// truss add docker writes the production images and truss deploy check
    /// verifies a target carries every value the modules demand, before the
    /// first crashloop teaches the list the hard way.
    /// </summary>
    public class DeployTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        [Fact]
        public void AddDocker_WritesAnImagePerHost_AndLaterHostsGetTheirs()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "docker", "--project", root));

            Assert.True(_workspace.FileExists("Shop", ".dockerignore"));

            var api = _workspace.ReadFile("Shop", "src", "Shop.Api", "Dockerfile");
            Assert.Contains("dotnet publish src/Shop.Api/Shop.Api.csproj", api);
            Assert.Contains("USER $APP_UID", api);
            Assert.Contains("HEALTHCHECK", api);
            Assert.Contains("ENTRYPOINT [\"dotnet\", \"Shop.Api.dll\"]", api);

            // Hosts born after the module get their image at birth.
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("split", "Sales", "--project", root));

            var worker = _workspace.ReadFile("Shop", "src", "Shop.Worker", "Dockerfile");
            Assert.Contains("dotnet/runtime:10.0-alpine", worker);
            Assert.DoesNotContain("HEALTHCHECK", worker);

            var service = _workspace.ReadFile("Shop", "src", "Shop.Sales.Api", "Dockerfile");
            Assert.Contains("ENTRYPOINT [\"dotnet\", \"Shop.Sales.Api.dll\"]", service);
        }

        [Fact]
        public void DeployCheck_ListsTheMissingValues_AndFails()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "email", "--provider", "resend", "--project", root));

            var envFile = Path.Combine(root, ".env.production");
            File.WriteAllText(envFile, "ConnectionStrings__Default=Data Source=/data/shop.db\n");

            Assert.Equal(1, _workspace.Run("deploy", "check", "--env-file", envFile, "--project", root));

            var output = _workspace.Capture("deploy", "check", "--env-file", envFile, "--project", root);
            Assert.Contains("ok     ", output);
            Assert.Contains("MISSING", output);
            Assert.Contains("Truss__Auth__Jwt__SigningKey", output);
            Assert.Contains("Truss__Email__Resend__ApiKey", output);
        }

        [Fact]
        public void DeployCheck_WithEverythingPresent_Passes()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));

            var envFile = Path.Combine(root, ".env.production");
            File.WriteAllText(envFile,
                "ConnectionStrings__Default=Data Source=/data/shop.db\n" +
                "Truss__Auth__Jwt__SigningKey=0123456789ABCDEF\n");

            Assert.Equal(0, _workspace.Run("deploy", "check", "--env-file", envFile, "--project", root));
        }

        [Fact]
        public void DeployCheck_WarnsAboutTransportAndDatabase_WhenTheConstellationOutgrowsThem()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("split", "Sales", "--project", root));

            var envFile = Path.Combine(root, ".env.production");
            File.WriteAllText(envFile, "ConnectionStrings__Default=Data Source=/data/shop.db\n");

            var output = _workspace.Capture("deploy", "check", "--env-file", envFile, "--project", root);

            Assert.Contains("inmemory transport does not cross processes", output);
            Assert.Contains("sqlite", output);
            Assert.Contains("Shop.Sales.Api is its own deployment", output);
        }

        [Fact]
        public void DeployInitSsh_GeneratesTheThreeArtifacts_ForTheWholeConstellation()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "postgres"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "messaging", "--transport", "redis", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("split", "Sales", "--project", root));

            // The ssh target ships images, so docker comes first.
            Assert.Equal(1, _workspace.Run("deploy", "init", "ssh", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "docker", "--project", root));
            Assert.Equal(0, _workspace.Run("deploy", "init", "ssh", "--project", root));

            var compose = _workspace.ReadFile("Shop", "deploy", "compose.production.yml");
            Assert.Contains("image: ${REGISTRY}/shop-api:${TAG}", compose);
            Assert.Contains("image: ${REGISTRY}/shop-sales:${TAG}", compose);
            Assert.Contains("image: ${REGISTRY}/shop-worker:${TAG}", compose);
            Assert.Contains("postgres:", compose);
            Assert.Contains("redis:", compose);
            Assert.Contains("postgres-data:", compose);

            var script = _workspace.ReadFile("Shop", "deploy", "deploy.sh");
            Assert.Contains("docker build -f \"src/Shop.Sales.Api/Dockerfile\"", script);
            Assert.Contains("truss deploy check --env-file", script);
            Assert.Contains("dotnet ef migrations bundle", script);
            Assert.Contains("rollback", script);

            var example = _workspace.ReadFile("Shop", "deploy", ".env.production.example");
            Assert.Contains("ConnectionStrings__Default=", example);
            Assert.Contains("ConnectionStrings__Redis=", example);
            Assert.Contains("POSTGRES_PASSWORD=", example);
        }

        public void Dispose()
        {
            _workspace.Dispose();
        }
    }
}
