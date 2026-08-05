using Xunit;

namespace Truss.Cli.Tests
{
    public class GenerateAndDoctorTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        private string ScaffoldShop()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            return _workspace.Root("Shop");
        }

        [Fact]
        public void GenerateCommand_CreatesRecordHandlerAndValidator()
        {
            var root = ScaffoldShop();

            var exitCode = _workspace.Run("generate", "command", "ArchiveProduct", "--context", "Catalog", "--project", root);

            Assert.Equal(0, exitCode);

            var command = _workspace.ReadFile("Shop", "src", "Shop.Application", "Catalog", "ArchiveProduct.cs");
            Assert.Contains("namespace Shop.Application.Catalog", command);
            Assert.Contains("public sealed record ArchiveProduct : ICommand;", command);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Catalog", "ArchiveProductHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Catalog", "ArchiveProductValidator.cs"));
        }

        [Fact]
        public void GenerateAggregate_CreatesIdAggregateAndEvent()
        {
            var root = ScaffoldShop();

            var exitCode = _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--project", root);

            Assert.Equal(0, exitCode);

            var aggregate = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Sales", "Order.cs");
            Assert.Contains("public class Order : AggregateRoot<OrderId>", aggregate);
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "OrderId.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "OrderCreated.cs"));
        }

        [Fact]
        public void GenerateQuery_UsesResultType()
        {
            var root = ScaffoldShop();

            var exitCode = _workspace.Run("generate", "query", "CountProducts", "--result", "int", "--project", root);

            Assert.Equal(0, exitCode);

            var query = _workspace.ReadFile("Shop", "src", "Shop.Application", "CountProducts.cs");
            Assert.Contains("IQuery<int>", query);
        }

        [Fact]
        public void GenerateExistingFile_Fails()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "command", "Ship", "--project", root));
            Assert.Equal(1, _workspace.Run("generate", "command", "Ship", "--project", root));
        }

        [Fact]
        public void Doctor_PassesOnFreshScaffold_AndFailsAfterDamage()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("doctor", "--project", root));

            File.Delete(Path.Combine(root, "src", "Shop.Domain", "Shop.Domain.csproj"));

            Assert.Equal(1, _workspace.Run("doctor", "--project", root));
        }

        public void Dispose() => _workspace.Dispose();
    }
}
