using Xunit;

namespace Truss.Cli.Tests
{
    public class AuthBindingTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        private string ScaffoldShop()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            return _workspace.Root("Shop");
        }

        [Fact]
        public void AddAuth_BindUserReference_PointsTheUserAtTheAggregate()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Customer", "--context", "Crm", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--bind-user", "Customer", "--project", root));

            var user = _workspace.ReadFile("Shop", "src", "Shop.Domain", "Accounts", "User", "User.cs");
            Assert.Contains("using Shop.Domain.Crm.Customer.ValueObjects;", user);
            Assert.Contains("public CustomerId CustomerId { get; private set; }", user);
            Assert.Contains("public static User Register(string email, string name, CustomerId customerId)", user);

            var command = _workspace.ReadFile("Shop", "src", "Shop.Application", "Accounts", "RegisterUser", "RegisterUser.cs");
            Assert.Contains("RegisterUser(string Email, string Name, string Password, Guid CustomerId)", command);

            var handler = _workspace.ReadFile("Shop", "src", "Shop.Application", "Accounts", "RegisterUser", "RegisterUserHandler.cs");
            Assert.Contains("User.Register(command.Email, command.Name, new CustomerId(command.CustomerId))", handler);

            var validator = _workspace.ReadFile("Shop", "src", "Shop.Application", "Accounts", "RegisterUser", "RegisterUserValidator.cs");
            Assert.Contains("RuleFor(command => command.CustomerId).NotEmpty();", validator);

            var login = _workspace.ReadFile("Shop", "src", "Shop.Application", "Accounts", "Login", "LoginHandler.cs");
            Assert.Contains("new Claim(\"customerId\", user.CustomerId.Value.ToString())", login);

            var configuration = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Accounts", "UserConfiguration.cs");
            Assert.Contains("builder.Property(user => user.CustomerId)", configuration);
            Assert.Contains("new CustomerId(value)", configuration);

            var manifest = TrussManifest.Load(root)!;
            Assert.Equal("Customer", manifest.Settings["auth.bind"]);
            Assert.Equal("reference", manifest.Settings["auth.bind.mode"]);
        }

        [Fact]
        public void AddAuth_BindUserMerge_AliasesTheAccountToTheAggregate()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Customer", "--context", "Crm", "--project", root));

            // The generated aggregate does not own identity fields yet, so merge
            // mode refuses and explains what to add.
            Assert.Equal(1, _workspace.Run("add", "auth", "--bind-user", "Customer", "--bind-mode", "merge", "--project", root));

            var path = Path.Combine(root, "src", "Shop.Domain", "Crm", "Customer", "Customer.cs");
            File.WriteAllText(path, """
                using Shop.Domain.Crm.Customer.ValueObjects;
                using Truss.Domain;

                namespace Shop.Domain.Crm.Customer
                {
                    public class Customer : AggregateRoot<CustomerId>
                    {
                        private Customer()
                        {
                        }

                        private Customer(CustomerId id) : base(id)
                        {
                        }

                        public string Email { get; private set; } = string.Empty;

                        public string Name { get; private set; } = string.Empty;

                        public static Customer Register(string email, string name)
                        {
                            return new Customer(new CustomerId(Guid.NewGuid()))
                            {
                                Email = email.ToLowerInvariant(),
                                Name = name
                            };
                        }
                    }
                }
                """);

            Assert.Equal(0, _workspace.Run("add", "auth", "--bind-user", "Customer", "--bind-mode", "merge", "--project", root));

            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Domain", "Accounts", "User", "User.cs"));
            Assert.False(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "UserConfiguration.cs"));

            var aliases = _workspace.ReadFile("Shop", "src", "Shop.Application", "Accounts", "AccountAliases.cs");
            Assert.Contains("global using User = Shop.Domain.Crm.Customer.Customer;", aliases);
            Assert.Contains("global using UserId = Shop.Domain.Crm.Customer.ValueObjects.CustomerId;", aliases);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "AccountAliases.cs"));

            // The aggregate had no EF configuration, so merge mode writes one
            // mapping the identity fields.
            var configuration = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "Accounts", "CustomerConfiguration.cs");
            Assert.Contains("IEntityTypeConfiguration<Customer>", configuration);
            Assert.Contains("builder.HasIndex(entity => entity.Email).IsUnique();", configuration);

            var manifest = TrussManifest.Load(root)!;
            Assert.Equal("merge", manifest.Settings["auth.bind.mode"]);
        }

        [Fact]
        public void AddAuth_External_WiresProvidersEndpointsAndStore()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "auth", "--external", "google,github", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains(".AddCookie(\"truss.external\")", program);
            Assert.Contains(".AddGoogle(\"google\", options =>", program);
            Assert.Contains(".AddGitHub(\"github\", options =>", program);
            Assert.Contains("app.MapExternalAuth();", program);
            Assert.Contains("using Shop.Api;", program);

            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Api", "ExternalAuthEndpoints.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Application", "Accounts", "ExternalLogin", "ExternalLoginHandler.cs"));
            Assert.True(_workspace.FileExists("Shop", "src", "Shop.Infrastructure", "Accounts", "EfExternalLoginStore.cs"));

            var module = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AccountsModule.cs");
            Assert.Contains("services.AddScoped<IExternalLoginStore, EfExternalLoginStore>();", module);

            var csproj = _workspace.ReadFile("Shop", "src", "Shop.Api", "Shop.Api.csproj");
            Assert.Contains("Microsoft.AspNetCore.Authentication.Google", csproj);
            Assert.Contains("AspNet.Security.OAuth.GitHub", csproj);

            var manifest = TrussManifest.Load(root)!;
            Assert.Equal("github,google", manifest.Settings["auth.external"]);
        }

        [Fact]
        public void AddAuth_ExternalAfterInstall_ChainsTheNewProvider()
        {
            var root = ScaffoldShop();

            Assert.Equal(0, _workspace.Run("add", "auth", "--external", "google", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--external", "microsoft", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains(".AddGoogle(\"google\", options =>", program);
            Assert.Contains(".AddMicrosoftAccount(\"microsoft\", options =>", program);

            var manifest = TrussManifest.Load(root)!;
            Assert.Equal("google,microsoft", manifest.Settings["auth.external"]);
        }

        [Fact]
        public void AddAuth_BindUser_RequiresAnExistingAggregate()
        {
            var root = ScaffoldShop();

            Assert.Equal(1, _workspace.Run("add", "auth", "--bind-user", "Missing", "--project", root));
        }

        public void Dispose() => _workspace.Dispose();
    }
}
