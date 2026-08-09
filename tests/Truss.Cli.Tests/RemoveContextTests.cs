using Xunit;

namespace Truss.Cli.Tests
{
    public class RemoveContextTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        [Fact]
        public void RemoveContext_DeletesTheFoldersAndUnwindsTheCrudWiring()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Invoice", "--context", "Billing", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("remove", "context", "Billing", "--project", root));

            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Domain", "Billing")));
            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Application", "Billing")));
            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Infrastructure", "Billing")));
            Assert.False(Directory.Exists(Path.Combine(root, "tests", "Shop.Domain.Tests", "Billing")));
            Assert.False(Directory.Exists(Path.Combine(root, "tests", "Shop.IntegrationTests", "Billing")));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.DoesNotContain("Invoice", program);
            Assert.DoesNotContain("using Shop.Application.Billing;", program);
            Assert.DoesNotContain("using Shop.Infrastructure.Billing;", program);
        }

        [Fact]
        public void RemoveContext_Catalog_RemovesTheSampleCompletely()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--sample"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("remove", "context", "Catalog", "--project", root));

            Assert.False(Directory.Exists(Path.Combine(root, "src", "Shop.Domain", "Catalog")));
            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "InfrastructureModule.cs"));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.DoesNotContain("Product", program);
            Assert.DoesNotContain("AddInfrastructure", program);
            Assert.DoesNotContain("using Shop.Application.Catalog;", program);

            var dbContext = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.DoesNotContain("Product", dbContext);
            Assert.DoesNotContain("using Shop.Domain.Catalog;", dbContext);

            var manifest = TrussManifest.Load(root);
            Assert.False(manifest!.Sample);
        }

        [Fact]
        public void RemoveContext_OfARootLevelAggregate_SweepsTheLooseTestAndEfFiles()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Gadget", "--crud", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.Domain.Tests", "GadgetTests.cs"));
            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.IntegrationTests", "GadgetCrudTests.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "EfGadgetRepository.cs"));

            Assert.Equal(0, _workspace.Run("remove", "context", "Gadget", "--project", root));

            Assert.False(_workspace.FileExists("Shop", "tests", "Shop.Domain.Tests", "GadgetTests.cs"));
            Assert.False(_workspace.FileExists("Shop", "tests", "Shop.IntegrationTests", "GadgetCrudTests.cs"));
            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "GadgetConfiguration.cs"));
            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "EfGadgetRepository.cs"));
            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.IntegrationTests", "HostSmokeTests.cs"));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.DoesNotContain("Gadget", program);
        }

        [Fact]
        public void RemoveContext_Catalog_LeavesOtherContextsAlone()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--sample"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Ticket", "--context", "Support", "--project", root));
            Assert.Equal(0, _workspace.Run("remove", "context", "Catalog", "--project", root));

            Assert.False(_workspace.FileExists("Shop", "tests", "Shop.Domain.Tests", "Catalog", "ProductTests.cs"));
            Assert.False(_workspace.FileExists("Shop", "tests", "Shop.IntegrationTests", "Catalog", "CatalogTests.cs"));
            Assert.True(_workspace.FileExists("Shop", "tests", "Shop.Domain.Tests", "Support", "TicketTests.cs"));
        }

        [Fact]
        public void RemoveContext_ThatDoesNotExist_Fails()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));

            Assert.Equal(1, _workspace.Run("remove", "context", "Billing", "--project", _workspace.Root("Shop")));
        }

        [Fact]
        public void RemoveContext_Accounts_RefusesWhileAuthIsInstalled()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(1, _workspace.Run("remove", "context", "Accounts", "--project", root));

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Domain", "Accounts", "User.cs"));
        }

        public void Dispose() => _workspace.Dispose();
    }
}
