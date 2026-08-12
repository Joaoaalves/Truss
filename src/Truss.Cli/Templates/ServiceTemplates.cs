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
                <PackageReference Include="Truss.Messaging.Abstractions" Version="__TRUSS_VERSION__" />
              </ItemGroup>

            </Project>
            """;

        public const string ContractsReadme = """
            namespace __NAME__.__CONTEXT__.Contracts
            {
                /// <summary>
                /// The events other services consume from the __CONTEXT__ service
                /// live here, so no service references another's internals. Move an
                /// integration event into this project the moment a second service
                /// needs it, and give it a stable wire name with
                /// [IntegrationEventName("__CONTEXTLOWER__.event-name")].
                /// </summary>
                public static class __CONTEXT__Contracts
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
