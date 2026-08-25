namespace Truss.Cli.Templates
{
    /// <summary>
    /// Templates for the host a split context receives: its own composition
    /// root, its own DbContext over the context's configurations, and the
    /// contracts project both sides share events through.
    /// </summary>
    internal static class ServiceTemplates
    {
        public const string DbContext = """
            using __NAME__.Infrastructure.__CONTEXT__;
            using Microsoft.EntityFrameworkCore;

            namespace __NAME__.__CONTEXT__.Api
            {
                public class __CONTEXT__DbContext(DbContextOptions<__CONTEXT__DbContext> options) : DbContext(options)
                {
                    protected override void OnModelCreating(ModelBuilder modelBuilder)
                    {
                        modelBuilder.ApplyConfigurationsFromAssembly(typeof(__CONTEXT__InfrastructureMarker).Assembly);__MODEL_EXTRAS__
                        // truss: model
                    }
                }
            }
            """;

        public const string ContractsCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <RootNamespace>__NAME__.__CONTEXT__.Contracts</RootNamespace>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Truss.Application" Version="__TRUSS_VERSION__" />__MESSAGING_PACKAGE__
              </ItemGroup>

            </Project>
            """;

        public const string ContractsReadme = """
            namespace __NAME__.__CONTEXT__.Contracts
            {
                /// <summary>
                /// What other services are allowed to know about __CONTEXT__ lives
                /// here, so no service references another's internals. Move an
                /// integration event into this project the moment a second service
                /// consumes it, with a stable wire name via
                /// [IntegrationEventName("__CONTEXTLOWER__.event-name")]. Queries
                /// other services may ask synchronously go here too, with their
                /// result DTOs: the __CONTEXT__ host serves them through
                /// MapRemoteContext and consumers wire them with
                /// AddRemoteContext&lt;__CONTEXT__Contracts&gt;. Commands stay out
                /// by design; a synchronous command between services is coupling
                /// in disguise.
                /// </summary>
                public sealed class __CONTEXT__Contracts
                {
                }
            }
            """;

        public const string LaunchSettings = """
            {
              "profiles": {
                "http": {
                  "commandName": "Project",
                  "applicationUrl": "http://localhost:__PORT__",
                  "environmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Development"
                  }
                }
              }
            }
            """;
    }
}
