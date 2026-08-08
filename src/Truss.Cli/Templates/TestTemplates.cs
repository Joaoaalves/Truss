namespace Truss.Cli.Templates
{
    /// <summary>
    /// Templates for the scaffolded test projects: pure domain tests and
    /// integration tests dispatching commands through the full pipeline on
    /// the TrussTestHost. Generators add matching tests per building block.
    /// </summary>
    internal static class TestTemplates
    {
        public const string DomainTestsCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <IsPackable>false</IsPackable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
                <PackageReference Include="xunit" Version="2.9.*" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
              </ItemGroup>

              <ItemGroup>
                <ProjectReference Include="..\..\src\__NAME__.Domain\__NAME__.Domain.csproj" />
              </ItemGroup>

            </Project>
            """;

        public const string IntegrationTestsCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <IsPackable>false</IsPackable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
                <PackageReference Include="Truss.Testing" Version="__TRUSS_VERSION__" />
                <PackageReference Include="xunit" Version="2.9.*" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
              </ItemGroup>

              <ItemGroup>
                <ProjectReference Include="..\..\src\__NAME__.Application\__NAME__.Application.csproj" />
                <ProjectReference Include="..\..\src\__NAME__.Infrastructure\__NAME__.Infrastructure.csproj" />
              </ItemGroup>

            </Project>
            """;

        public const string IntegrationTestsCsprojWithoutInfrastructure = """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <IsPackable>false</IsPackable>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
                <PackageReference Include="Truss.Testing" Version="__TRUSS_VERSION__" />
                <PackageReference Include="xunit" Version="2.9.*" />
                <PackageReference Include="xunit.runner.visualstudio" Version="2.8.*" />
              </ItemGroup>

              <ItemGroup>
                <ProjectReference Include="..\..\src\__NAME__.Application\__NAME__.Application.csproj" />
              </ItemGroup>

            </Project>
            """;

        public const string SolutionTestsFolder = """
              <Folder Name="/tests/">
                <Project Path="tests/__NAME__.Domain.Tests/__NAME__.Domain.Tests.csproj" />
                <Project Path="tests/__NAME__.IntegrationTests/__NAME__.IntegrationTests.csproj" />
              </Folder>
            """;

        public const string HostSmokeTests = """
            using __NAME__.Application;
            using __NAME__.Infrastructure;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.Extensions.DependencyInjection;
            using Truss.Testing;
            using Xunit;

            namespace __NAME__.IntegrationTests
            {
                public class HostSmokeTests
                {
                    [Fact]
                    public async Task TheHost_BootsThePipelineAndTheDatabase()
                    {
                        // The host runs the full pipeline over a throwaway sqlite
                        // database. Register the repositories a slice under test
                        // needs with options.ConfigureServices.
                        await using var host = await TrussTestHost.Start<AppDbContext>(options =>
                        {
                            options.AddAssembly<ApplicationAssemblyMarker>();
                        });

                        var canConnect = await host.ExecuteScoped(provider =>
                            provider.GetRequiredService<AppDbContext>().Database.CanConnectAsync());

                        Assert.True(canConnect);
                    }
                }
            }
            """;

        public const string HostSmokeTestsWithoutDatabase = """
            using __NAME__.Application;
            using Truss.Testing;
            using Xunit;

            namespace __NAME__.IntegrationTests
            {
                public class HostSmokeTests
                {
                    [Fact]
                    public async Task TheHost_BootsThePipeline()
                    {
                        await using var host = await TrussTestHost.Start(options =>
                        {
                            options.AddAssembly<ApplicationAssemblyMarker>();
                        });

                        Assert.NotNull(host.Services);
                    }
                }
            }
            """;

        public const string SampleProductTests = """
            using __NAME__.Domain.Catalog;
            using Truss.Domain;
            using Xunit;

            namespace __NAME__.Domain.Tests.Catalog
            {
                public class ProductTests
                {
                    [Fact]
                    public void Create_RaisesTheCreationEvent()
                    {
                        var product = Product.Create("Beam", 10m);

                        Assert.Equal("Beam", product.Name);
                        Assert.Contains(product.DomainEvents, domainEvent => domainEvent is ProductCreated);
                    }

                    [Fact]
                    public void Create_WithoutAPositivePrice_BreaksTheRule()
                    {
                        Assert.Throws<BusinessRuleValidationException>(() => Product.Create("Beam", 0m));
                    }
                }
            }
            """;

        public const string SampleCatalogTests = """
            using __NAME__.Application;
            using __NAME__.Application.Catalog;
            using __NAME__.Infrastructure;
            using __NAME__.Infrastructure.Catalog;
            using Microsoft.Extensions.DependencyInjection;
            using Truss.Application;
            using Truss.Testing;
            using Xunit;

            namespace __NAME__.IntegrationTests.Catalog
            {
                public class CatalogTests
                {
                    private static Task<TrussTestHost> StartHost()
                    {
                        return TrussTestHost.Start<AppDbContext>(options =>
                        {
                            options.AddAssembly<ApplicationAssemblyMarker>();
                            options.ConfigureServices(services => services.AddScoped<IProductRepository, EfProductRepository>());
                        });
                    }

                    [Fact]
                    public async Task CreateProduct_PersistsAndReadsBack()
                    {
                        await using var host = await StartHost();

                        var id = await host.Send(new CreateProduct("Beam", 10m));
                        var product = await host.Send(new GetProductById(id));

                        Assert.Equal("Beam", product!.Name);
                    }

                    [Fact]
                    public async Task CreateProduct_WithAnInvalidPrice_FailsValidation()
                    {
                        await using var host = await StartHost();

                        await Assert.ThrowsAsync<RequestValidationException>(() => host.Send(new CreateProduct("Beam", 0m)));
                    }
                }
            }
            """;

        public const string AggregateTests = """
            using __NS_AGG__;
            using __NS_AGG__.Events;
            using Xunit;

            namespace __NS_DOMAIN_TESTS__
            {
                public class __TYPE__Tests
                {
                    [Fact]
                    public void Create_RaisesTheCreationEvent()
                    {
                        var __CAMEL__ = __TYPE__.Create();

                        Assert.Contains(__CAMEL__.DomainEvents, domainEvent => domainEvent is __TYPE__Created);
                    }
                }
            }
            """;

        public const string AggregateCrudTests = """
            using __NS_AGG__;
            using __NS_AGG__.Events;
            using Xunit;

            namespace __NS_DOMAIN_TESTS__
            {
                public class __TYPE__Tests
                {
                    [Fact]
                    public void Create_SetsTheName_AndRaisesTheCreationEvent()
                    {
                        var __CAMEL__ = __TYPE__.Create("Beam");

                        Assert.Equal("Beam", __CAMEL__.Name);
                        Assert.Contains(__CAMEL__.DomainEvents, domainEvent => domainEvent is __TYPE__Created);
                    }

                    [Fact]
                    public void Rename_ChangesTheName()
                    {
                        var __CAMEL__ = __TYPE__.Create("Beam");

                        __CAMEL__.Rename("Joist");

                        Assert.Equal("Joist", __CAMEL__.Name);
                    }
                }
            }
            """;

        public const string CrudIntegrationTests = """
            using __NAME__.Application;
            using __NAME__.Infrastructure;
            using __NS_FEATURE__;
            using __NS_FEATURE__.Create__TYPE__;
            using __NS_FEATURE__.Delete__TYPE__;
            using __NS_FEATURE__.Get__TYPE__ById;
            using __NS_FEATURE__.List__TYPE__;
            using __NS_FEATURE__.Update__TYPE__;
            using __NS_INFRASTRUCTURE__;
            using Microsoft.Extensions.DependencyInjection;
            using Truss.Testing;
            using Xunit;

            namespace __NS_INTEGRATION_TESTS__
            {
                public class __TYPE__CrudTests
                {
                    private static Task<TrussTestHost> StartHost()
                    {
                        return TrussTestHost.Start<AppDbContext>(options =>
                        {
                            options.AddAssembly<ApplicationAssemblyMarker>();
                            options.ConfigureServices(services => services.AddScoped<I__TYPE__Repository, Ef__TYPE__Repository>());
                        });
                    }

                    [Fact]
                    public async Task TheSlice_CreatesReadsUpdatesAndDeletes()
                    {
                        await using var host = await StartHost();

                        var id = await host.Send(new Create__TYPE__("Beam"));

                        var created = await host.Send(new Get__TYPE__ById(id));
                        Assert.Equal("Beam", created!.Name);

                        await host.Send(new Update__TYPE__(id, "Joist"));

                        var page = await host.Send(new List__TYPE__());
                        Assert.Contains(page.Items, item => item.Name == "Joist");

                        await host.Send(new Delete__TYPE__(id));

                        Assert.Null(await host.Send(new Get__TYPE__ById(id)));
                    }
                }
            }
            """;
    }
}
