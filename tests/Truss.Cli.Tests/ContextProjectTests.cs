using Xunit;

namespace Truss.Cli.Tests
{
    /// <summary>
    /// A bounded context can live in its own projects, the first step of the
    /// extraction path. Namespaces never change between the two layouts, so
    /// moving an existing context is a file move and the rest of the code
    /// stays untouched.
    /// </summary>
    public class ContextProjectTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        private string ScaffoldShop()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            return _workspace.Root("Shop");
        }

        [Fact]
        public void CreateAsProjects_ScaffoldsTheThreeProjectsAndWiresThem()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "context", "Sales", "--as-projects", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Sales.Domain", "Shop.Sales.Domain.csproj"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Sales.Application", "Shop.Sales.Application.csproj"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Sales.Infrastructure", "Shop.Sales.Infrastructure.csproj"));

            var marker = _workspace.ReadFile("Shop", "src", "Shop.Sales.Application", "SalesAssemblyMarker.cs");
            Assert.Contains("namespace Shop.Application.Sales", marker);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("options.AddAssembly<SalesAssemblyMarker>();", program);
            Assert.Contains("using Shop.Application.Sales;", program);

            var dbContext = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.Contains("ApplyConfigurationsFromAssembly(typeof(SalesInfrastructureMarker).Assembly);", dbContext);

            var infrastructureCsproj = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Shop.Infrastructure.csproj");
            Assert.Contains("Shop.Sales.Infrastructure.csproj", infrastructureCsproj);

            var solution = _workspace.ReadFile("Shop", "Shop.slnx");
            Assert.Contains("src/Shop.Sales.Domain/Shop.Sales.Domain.csproj", solution);

            var domainTests = _workspace.ReadFile("Shop", "tests", "Shop.Domain.Tests", "Shop.Domain.Tests.csproj");
            Assert.Contains("Shop.Sales.Domain.csproj", domainTests);
        }

        [Fact]
        public void Generators_TargetTheContextProjects_WithTheSameNamespaces()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "context", "Sales", "--as-projects", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--crud", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Sales.Domain", "Order", "Order.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Sales.Application", "Order", "CreateOrder", "CreateOrder.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Sales.Infrastructure", "EfOrderRepository.cs"));

            var aggregate = _workspace.ReadFile("Shop", "src", "Shop.Sales.Domain", "Order", "Order.cs");
            Assert.Contains("namespace Shop.Domain.Sales.Order", aggregate);

            var repository = _workspace.ReadFile("Shop", "src", "Shop.Sales.Infrastructure", "EfOrderRepository.cs");
            Assert.Contains("namespace Shop.Infrastructure.Sales", repository);
            Assert.Contains("EfOrderRepository(DbContext context)", repository);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddScoped<IOrderRepository, EfOrderRepository>();", program);
            Assert.Contains("MapPutCommand<UpdateOrder>", program);
        }

        [Fact]
        public void MoveExisting_LiftsTheSliceWithoutTouchingNamespaces()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Shipment", "--context", "Shipping", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "context", "Shipping", "--as-projects", "--project", root));

            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Domain", "Shipping")));
            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Application", "Shipping")));
            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Infrastructure", "Shipping")));

            var aggregate = _workspace.ReadFile("Shop", "src", "Shop.Shipping.Domain", "Shipment", "Shipment.cs");
            Assert.Contains("namespace Shop.Domain.Shipping.Shipment", aggregate);

            var repository = _workspace.ReadFile("Shop", "src", "Shop.Shipping.Infrastructure", "EfShipmentRepository.cs");
            Assert.Contains("EfShipmentRepository(DbContext context)", repository);
            Assert.DoesNotContain("AppDbContext", repository);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("options.AddAssembly<ShippingAssemblyMarker>();", program);
        }

        [Fact]
        public void ModulesInstalledLater_RegisterTheContextInTheirBlocksToo()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "context", "Sales", "--as-projects", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "jobs", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            var registrations = program.Split("options.AddAssembly<SalesAssemblyMarker>();").Length - 1;

            Assert.Equal(3, registrations);
        }

        [Fact]
        public void Remove_UnwindsAProjectContextCompletely()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "context", "Sales", "--as-projects", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("remove", "context", "Sales", "--project", root));

            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Sales.Domain")));
            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Sales.Application")));
            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Sales.Infrastructure")));

            Assert.DoesNotContain("Sales", _workspace.ReadFile("Shop", "Shop.slnx"));
            Assert.DoesNotContain("Sales", _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs"));
            Assert.DoesNotContain("Sales", _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs"));
            Assert.DoesNotContain("Sales", _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Shop.Infrastructure.csproj"));
        }

        [Fact]
        public void AContextHoldingTheBoundAccountAggregate_StaysPut()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Customer", "--context", "Crm", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--bind-user", "Customer", "--project", root));

            var output = _workspace.Capture("generate", "context", "Crm", "--as-projects", "--project", root);

            Assert.Contains("bound", output);
            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Crm.Domain")));
        }

        public void Dispose()
        {
            _workspace.Dispose();
        }
    }
}
