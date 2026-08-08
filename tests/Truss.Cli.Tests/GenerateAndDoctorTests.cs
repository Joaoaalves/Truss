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
        public void GenerateAggregate_CreatesItsFolder_WithIdEventAndRule()
        {
            var root = ScaffoldShop();

            var exitCode = _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--project", root);

            Assert.Equal(0, exitCode);

            var aggregate = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Sales", "Order", "Order.cs");
            Assert.Contains("public class Order : AggregateRoot<OrderId>", aggregate);
            Assert.Contains("CheckRule(new OrderMustBeValid())", aggregate);
            Assert.Contains("namespace Shop.Domain.Sales", aggregate);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Order", "ValueObjects", "OrderId.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Order", "Events", "OrderCreated.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Order", "Rules", "OrderMustBeValid.cs"));
        }

        [Fact]
        public void GenerateEntity_CreatesEntityWithTypedId_AloneOrInsideAnAggregate()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "entity", "Warehouse", "--context", "Sales", "--project", root));

            var entity = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Sales", "Warehouse", "Warehouse.cs");
            Assert.Contains("public class Warehouse : Entity<WarehouseId>", entity);
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Warehouse", "ValueObjects", "WarehouseId.cs"));

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "entity", "OrderItem", "--context", "Sales", "--aggregate", "Order", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Order", "OrderItem.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Order", "ValueObjects", "OrderItemId.cs"));
        }

        [Fact]
        public void GenerateAggregate_WithCrud_GeneratesCommandsQueriesRepositoryAndRoutes()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Invoice", "--context", "Billing", "--crud", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "CreateInvoiceHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "UpdateInvoiceHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "DeleteInvoiceHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "GetInvoiceById.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "ListInvoice.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Billing", "InvoiceConfiguration.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Billing", "EfInvoiceRepository.cs"));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddScoped<IInvoiceRepository, EfInvoiceRepository>", program);
            Assert.Contains("app.MapCommand<CreateInvoice, Guid>(\"/invoices\"", program);
            Assert.Contains("app.MapQuery<ListInvoice, PageResult<InvoiceDto>>(\"/invoices\");", program);
            Assert.Contains("using Shop.Application.Billing;", program);
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
        public void GeneratePagedQuery_CreatesRecordHandlerAndValidator()
        {
            var root = ScaffoldShop();

            var exitCode = _workspace.Run("generate", "query", "ListProducts", "--context", "Catalog", "--result", "Guid", "--paged", "--project", root);

            Assert.Equal(0, exitCode);

            var query = _workspace.ReadFile("Shop", "src", "Shop.Application", "Catalog", "ListProducts.cs");
            Assert.Contains("record ListProducts(int Page = 1, int Size = 20) : IQuery<PageResult<Guid>>", query);

            var handler = _workspace.ReadFile("Shop", "src", "Shop.Application", "Catalog", "ListProductsHandler.cs");
            Assert.Contains("IQueryHandler<ListProducts, PageResult<Guid>>", handler);
            Assert.Contains("ToPageAsync", handler);

            var validator = _workspace.ReadFile("Shop", "src", "Shop.Application", "Catalog", "ListProductsValidator.cs");
            Assert.Contains("InclusiveBetween(1, 100)", validator);
        }

        [Fact]
        public void GenerateExistingFile_Fails()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "command", "Ship", "--project", root));
            Assert.Equal(1, _workspace.Run("generate", "command", "Ship", "--project", root));
        }

        [Fact]
        public void Update_PointsEveryTrussPackageAtTheCliVersion()
        {
            var root = ScaffoldShop();
            var current = TrussVersionInfo.Current();

            var csproj = Path.Combine(root, "src", "Shop.Domain", "Shop.Domain.csproj");
            File.WriteAllText(csproj, File.ReadAllText(csproj).Replace(current, "0.0.1"));

            Assert.Equal(0, _workspace.Run("update", "--project", root));

            var updated = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Shop.Domain.csproj");
            Assert.DoesNotContain("0.0.1", updated);
            Assert.Contains($"Version=\"{current}\"", updated);

            var manifest = TrussManifest.Load(root);
            Assert.Equal(current, manifest!.TrussVersion);
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
