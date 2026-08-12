using Xunit;

namespace Truss.Cli.Tests
{
    /// <summary>
    /// truss split extracts a bounded context into its own service. The
    /// context's routes and registrations move from the monolith's Program
    /// into the new host; the handlers never change. By default the service
    /// owns its database, and --shared-database keeps the monolith's.
    /// </summary>
    public class SplitTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        private string ScaffoldWithSlice()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--crud", "--project", root));

            return root;
        }

        [Fact]
        public void Split_MovesTheContextIntoItsOwnService()
        {
            var root = ScaffoldWithSlice();

            Assert.Equal(0, _workspace.Run("split", "Sales", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Sales.Api", "Program.cs");
            Assert.Contains("AddDbContext<SalesDbContext>", program);
            Assert.Contains("options.AddAssembly<SalesAssemblyMarker>();", program);
            Assert.Contains("AddTrussOutbox<SalesDbContext>", program);
            Assert.Contains("AddTrussInbox<SalesDbContext>", program);
            Assert.Contains("AddScoped<IOrderRepository, EfOrderRepository>();", program);
            Assert.Contains("MapPutCommand<UpdateOrder>", program);

            var mainProgram = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.DoesNotContain("SalesAssemblyMarker", mainProgram);
            Assert.DoesNotContain("IOrderRepository", mainProgram);
            Assert.DoesNotContain("MapPutCommand<UpdateOrder>", mainProgram);

            var dbContext = _workspace.ReadFile("Shop", "src", "Shop.Sales.Api", "SalesDbContext.cs");
            Assert.Contains("ApplyConfigurationsFromAssembly(typeof(SalesInfrastructureMarker).Assembly);", dbContext);
            Assert.Contains("ApplyTrussOutbox();", dbContext);

            var mainDbContext = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.DoesNotContain("SalesInfrastructureMarker", mainDbContext);

            var settings = _workspace.ReadFile("Shop", "src", "Shop.Sales.Api", "appsettings.json");
            Assert.Contains("shop-sales.db", settings);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Sales.Contracts", "Shop.Sales.Contracts.csproj"));

            var manifest = TrussManifest.Load(root);
            Assert.Equal("own-db", manifest!.Settings["service.Sales"]);
        }

        [Fact]
        public void Split_WithASharedDatabase_LeavesTheSchemaWithTheMonolith()
        {
            var root = ScaffoldWithSlice();

            Assert.Equal(0, _workspace.Run("split", "Sales", "--shared-database", "--project", root));

            var mainSettings = _workspace.ReadFile("Shop", "src", "Shop.Api", "appsettings.json");
            var serviceSettings = _workspace.ReadFile("Shop", "src", "Shop.Sales.Api", "appsettings.json");
            Assert.Contains("shop.db", serviceSettings);
            Assert.Contains("shop.db", mainSettings);

            var mainDbContext = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.Contains("SalesInfrastructureMarker", mainDbContext);

            var program = _workspace.ReadFile("Shop", "src", "Shop.Sales.Api", "Program.cs");
            Assert.Contains("never migrates", program);
            Assert.DoesNotContain("EnsureCreated", program);

            var manifest = TrussManifest.Load(root);
            Assert.Equal("shared-db", manifest!.Settings["service.Sales"]);
        }

        [Fact]
        public void Split_OfAnUnknownContext_Fails()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));

            Assert.Equal(1, _workspace.Run("split", "Ghost", "--project", _workspace.Root("Shop")));
        }

        public void Dispose()
        {
            _workspace.Dispose();
        }
    }
}
