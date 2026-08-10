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

            var command = _workspace.ReadFile("Shop", "src", "Shop.Application", "Catalog", "ArchiveProduct", "ArchiveProduct.cs");
            Assert.Contains("namespace Shop.Application.Catalog.ArchiveProduct", command);
            Assert.Contains("public sealed record ArchiveProduct : ICommand;", command);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Catalog", "ArchiveProduct", "ArchiveProductHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Catalog", "ArchiveProduct", "ArchiveProductValidator.cs"));
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
            Assert.Contains("namespace Shop.Domain.Sales.Order", aggregate);
            Assert.Contains("using Shop.Domain.Sales.Order.ValueObjects;", aggregate);

            var id = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Sales", "Order", "ValueObjects", "OrderId.cs");
            Assert.Contains("namespace Shop.Domain.Sales.Order.ValueObjects", id);
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Order", "Events", "OrderCreated.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Order", "Rules", "OrderMustBeValid.cs"));

            var test = _workspace.ReadFile("Shop", "tests", "Shop.Domain.Tests", "Sales", "OrderTests.cs");
            Assert.Contains("namespace Shop.Domain.Tests.Sales", test);
            Assert.Contains("Order.Create()", test);
        }

        [Fact]
        public void GenerateEntity_CreatesEntityWithTypedId_AloneOrInsideAnAggregate()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "entity", "Warehouse", "--context", "Sales", "--project", root));

            var entity = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Sales", "Warehouse", "Warehouse.cs");
            Assert.Contains("public class Warehouse : Entity<WarehouseId>", entity);
            Assert.Contains("namespace Shop.Domain.Sales.Warehouse", entity);
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Warehouse", "ValueObjects", "WarehouseId.cs"));

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "entity", "OrderItem", "--context", "Sales", "--aggregate", "Order", "--project", root));

            var nested = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Sales", "Order", "OrderItem.cs");
            Assert.Contains("namespace Shop.Domain.Sales.Order", nested);

            var nestedId = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Sales", "Order", "ValueObjects", "OrderItemId.cs");
            Assert.Contains("namespace Shop.Domain.Sales.Order.ValueObjects", nestedId);
        }

        [Fact]
        public void GenerateAggregate_WithCrud_GeneratesCommandsQueriesRepositoryAndRoutes()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Invoice", "--context", "Billing", "--crud", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "DTOs", "InvoiceDto.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "Rules", "InvoiceMustExist.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "CreateInvoice", "CreateInvoice.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "CreateInvoice", "CreateInvoiceHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "CreateInvoice", "CreateInvoiceValidator.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "UpdateInvoice", "UpdateInvoiceHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "DeleteInvoice", "DeleteInvoiceHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "GetInvoiceById", "GetInvoiceById.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "GetInvoiceById", "GetInvoiceByIdHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "ListInvoice", "ListInvoice.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "ListInvoice", "ListInvoiceHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Billing", "Invoice", "ListInvoice", "ListInvoiceValidator.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Billing", "InvoiceConfiguration.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Billing", "EfInvoiceRepository.cs"));

            var dto = _workspace.ReadFile("Shop", "src", "Shop.Application", "Billing", "Invoice", "DTOs", "InvoiceDto.cs");
            Assert.Contains("namespace Shop.Application.Billing.Invoice.DTOs", dto);

            var create = _workspace.ReadFile("Shop", "src", "Shop.Application", "Billing", "Invoice", "CreateInvoice", "CreateInvoice.cs");
            Assert.Contains("namespace Shop.Application.Billing.Invoice.CreateInvoice", create);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddScoped<IInvoiceRepository, EfInvoiceRepository>", program);
            Assert.Contains("app.MapCommand<CreateInvoice, Guid>(\"/invoices\"", program);
            Assert.Contains("app.MapQuery<ListInvoice, PageResult<InvoiceDto>>(\"/invoices\");", program);
            Assert.Contains("using Shop.Application.Billing.Invoice.CreateInvoice;", program);
            Assert.Contains("using Shop.Application.Billing.Invoice.DTOs;", program);

            var domainTest = _workspace.ReadFile("Shop", "tests", "Shop.Domain.Tests", "Billing", "InvoiceTests.cs");
            Assert.Contains("Rename_ChangesTheName", domainTest);

            var crudTest = _workspace.ReadFile("Shop", "tests", "Shop.IntegrationTests", "Billing", "InvoiceCrudTests.cs");
            Assert.Contains("AddScoped<IInvoiceRepository, EfInvoiceRepository>", crudTest);
            Assert.Contains("new CreateInvoice(\"Beam\")", crudTest);
        }

        [Fact]
        public void ShortAliases_DoTheSameAsTheFullCommands()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("g", "ctx", "Sales", "--project", root));
            Assert.Equal(0, _workspace.Run("g", "agg", "Order", "--context", "Sales", "--project", root));
            Assert.Equal(0, _workspace.Run("g", "ent", "OrderItem", "--context", "Sales", "--aggregate", "Order", "--project", root));
            Assert.Equal(0, _workspace.Run("gen", "cmd", "ShipOrder", "--context", "Sales", "--project", root));
            Assert.Equal(0, _workspace.Run("g", "qry", "CountOrders", "--context", "Sales", "--result", "int", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Order", "Order.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Sales", "Order", "OrderItem.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Sales", "ShipOrder", "ShipOrderHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Sales", "CountOrders", "CountOrdersHandler.cs"));

            Assert.Equal(0, _workspace.Run("rm", "ctx", "Sales", "--project", root));

            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Domain", "Sales")));
        }

        [Fact]
        public void GenerateQuery_UsesResultType()
        {
            var root = ScaffoldShop();

            var exitCode = _workspace.Run("generate", "query", "CountProducts", "--result", "int", "--project", root);

            Assert.Equal(0, exitCode);

            var query = _workspace.ReadFile("Shop", "src", "Shop.Application", "CountProducts", "CountProducts.cs");
            Assert.Contains("IQuery<int>", query);
            Assert.Contains("namespace Shop.Application.CountProducts", query);
        }

        [Fact]
        public void GeneratePagedQuery_CreatesRecordHandlerAndValidator()
        {
            var root = ScaffoldShop();

            var exitCode = _workspace.Run("generate", "query", "ListProducts", "--context", "Catalog", "--result", "Guid", "--paged", "--project", root);

            Assert.Equal(0, exitCode);

            var query = _workspace.ReadFile("Shop", "src", "Shop.Application", "Catalog", "ListProducts", "ListProducts.cs");
            Assert.Contains("record ListProducts(int Page = 1, int Size = 20) : IQuery<PageResult<Guid>>", query);
            Assert.Contains("namespace Shop.Application.Catalog.ListProducts", query);

            var handler = _workspace.ReadFile("Shop", "src", "Shop.Application", "Catalog", "ListProducts", "ListProductsHandler.cs");
            Assert.Contains("IQueryHandler<ListProducts, PageResult<Guid>>", handler);
            Assert.Contains("ToPageAsync", handler);

            var validator = _workspace.ReadFile("Shop", "src", "Shop.Application", "Catalog", "ListProducts", "ListProductsValidator.cs");
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
