namespace Truss.Cli.Templates
{
    internal static class ProjectTemplates
    {
        public const string GitIgnore = """
            bin/
            obj/
            artifacts/
            *.user
            .vs/
            .idea/
            *.db
            """;

        public const string DirectoryBuildProps = """
            <Project>

              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>

            </Project>
            """;

        public const string SolutionWithInfrastructure = """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/__NAME__.Domain/__NAME__.Domain.csproj" />
                <Project Path="src/__NAME__.Application/__NAME__.Application.csproj" />
                <Project Path="src/__NAME__.Infrastructure/__NAME__.Infrastructure.csproj" />
                <Project Path="src/__NAME__.Api/__NAME__.Api.csproj" />
              </Folder>
            </Solution>
            """;

        public const string SolutionWithoutInfrastructure = """
            <Solution>
              <Folder Name="/src/">
                <Project Path="src/__NAME__.Domain/__NAME__.Domain.csproj" />
                <Project Path="src/__NAME__.Application/__NAME__.Application.csproj" />
                <Project Path="src/__NAME__.Api/__NAME__.Api.csproj" />
              </Folder>
            </Solution>
            """;

        public const string DomainCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">

              <ItemGroup>
                <PackageReference Include="Truss.Domain" Version="__TRUSS_VERSION__" />
              </ItemGroup>

            </Project>
            """;

        public const string ApplicationCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">

              <ItemGroup>
                <ProjectReference Include="..\__NAME__.Domain\__NAME__.Domain.csproj" />
              </ItemGroup>

              <ItemGroup>
                <PackageReference Include="Truss.Application.Abstractions" Version="__TRUSS_VERSION__" />
                <PackageReference Include="FluentValidation" Version="12.*" />
              </ItemGroup>

            </Project>
            """;

        public const string InfrastructureCsproj = """
            <Project Sdk="Microsoft.NET.Sdk">

              <ItemGroup>
                <ProjectReference Include="..\__NAME__.Application\__NAME__.Application.csproj" />
              </ItemGroup>

              <ItemGroup>
                <PackageReference Include="Truss.Persistence.EntityFrameworkCore" Version="__TRUSS_VERSION__" />
                <PackageReference Include="__EF_PROVIDER_PACKAGE__" Version="10.*" />__SQLITE_NATIVE_REFERENCE__
              </ItemGroup>

            </Project>
            """;

        public const string ApiCsprojWithInfrastructure = """
            <Project Sdk="Microsoft.NET.Sdk.Web">

              <ItemGroup>
                <ProjectReference Include="..\__NAME__.Application\__NAME__.Application.csproj" />
                <ProjectReference Include="..\__NAME__.Infrastructure\__NAME__.Infrastructure.csproj" />
              </ItemGroup>

              <ItemGroup>
                <PackageReference Include="Truss.Application" Version="__TRUSS_VERSION__" />
                <PackageReference Include="Truss.AspNetCore" Version="__TRUSS_VERSION__" />
                <PackageReference Include="Truss.Generators" Version="__TRUSS_VERSION__" PrivateAssets="all" />
                <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.*" />
                <PackageReference Include="Microsoft.OpenApi" Version="2.*" />
                <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.*" PrivateAssets="all" />
                <PackageReference Include="Scalar.AspNetCore" Version="2.*" />
              </ItemGroup>

            </Project>
            """;

        public const string ApiCsprojWithoutInfrastructure = """
            <Project Sdk="Microsoft.NET.Sdk.Web">

              <ItemGroup>
                <ProjectReference Include="..\__NAME__.Application\__NAME__.Application.csproj" />
              </ItemGroup>

              <ItemGroup>
                <PackageReference Include="Truss.Application" Version="__TRUSS_VERSION__" />
                <PackageReference Include="Truss.AspNetCore" Version="__TRUSS_VERSION__" />
                <PackageReference Include="Truss.Generators" Version="__TRUSS_VERSION__" PrivateAssets="all" />
                <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.*" />
                <PackageReference Include="Microsoft.OpenApi" Version="2.*" />
                <PackageReference Include="Scalar.AspNetCore" Version="2.*" />
              </ItemGroup>

            </Project>
            """;

        public const string ApplicationAssemblyMarker = """
            namespace __NAME__.Application
            {
                public sealed class ApplicationAssemblyMarker
                {
                }
            }
            """;

        public const string AppDbContextEmpty = """
            using Microsoft.EntityFrameworkCore;

            namespace __NAME__.Infrastructure
            {
                public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
                {
                    protected override void OnModelCreating(ModelBuilder modelBuilder)
                    {
                        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
                    }
                }
            }
            """;

        public const string AppDbContextSample = """
            using __NAME__.Domain.Catalog;
            using Microsoft.EntityFrameworkCore;

            namespace __NAME__.Infrastructure
            {
                public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
                {
                    public DbSet<Product> Products => Set<Product>();

                    protected override void OnModelCreating(ModelBuilder modelBuilder)
                    {
                        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
                    }
                }
            }
            """;

        public const string ProgramWithInfrastructure = """
            using __NAME__.Application;
            using __NAME__.Infrastructure;
            using Microsoft.EntityFrameworkCore;
            using Scalar.AspNetCore;

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.__EF_PROVIDER_METHOD__(builder.Configuration.GetConnectionString("Default")));

            builder.Services.AddTruss(options =>
            {
                options.AddAssembly<ApplicationAssemblyMarker>();
            });

            builder.Services.AddTrussEntityFramework<AppDbContext>();
            builder.Services.AddOpenApi();
            builder.Services.AddHealthChecks().AddTrussDatabase<AppDbContext>();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var database = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database;

                if (database.GetMigrations().Any())
                    database.Migrate();
                else
                    database.EnsureCreated();

                await app.Services.RunTrussSeeders();

                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.MapHealthChecks("/health");
            app.MapGet("/", () => "__NAME__ is running.");

            app.Run();
            """;

        public const string ProgramWithoutInfrastructure = """
            using __NAME__.Application;
            using Scalar.AspNetCore;

            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddTruss(options =>
            {
                options.AddAssembly<ApplicationAssemblyMarker>();
            });

            builder.Services.AddOpenApi();
            builder.Services.AddHealthChecks();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.MapScalarApiReference();
            }

            app.MapHealthChecks("/health");
            app.MapGet("/", () => "__NAME__ is running.");

            app.Run();
            """;

        public const string AppSettings = """
            {
              "ConnectionStrings": {
                "Default": "__CONNECTION_STRING__"
              },
              "Logging": {
                "LogLevel": {
                  "Default": "Information",
                  "Microsoft.AspNetCore": "Warning",
                  "Microsoft.EntityFrameworkCore": "Warning"
                }
              },
              "AllowedHosts": "*"
            }
            """;

        public const string AppSettingsWithoutDatabase = """
            {
              "Logging": {
                "LogLevel": {
                  "Default": "Information",
                  "Microsoft.AspNetCore": "Warning"
                }
              },
              "AllowedHosts": "*"
            }
            """;

        public const string ToolsManifest = """
            {
              "version": 1,
              "isRoot": true,
              "tools": {
                "dotnet-ef": {
                  "version": "10.0.10",
                  "commands": [
                    "dotnet-ef"
                  ],
                  "rollForward": true
                }
              }
            }
            """;

        public const string LaunchSettings = """
            {
              "profiles": {
                "http": {
                  "commandName": "Project",
                  "applicationUrl": "http://localhost:5000",
                  "environmentVariables": {
                    "ASPNETCORE_ENVIRONMENT": "Development"
                  }
                }
              }
            }
            """;

        public const string NuGetConfig = """
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <add key="local" value="__LOCAL_FEED__" />
                <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
              </packageSources>
            </configuration>
            """;
    }
}
