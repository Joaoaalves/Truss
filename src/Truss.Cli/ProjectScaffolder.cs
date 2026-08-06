using Truss.Cli.Templates;

namespace Truss.Cli
{
    internal sealed record ScaffoldOptions(
        string Name,
        string Database,
        bool Docker,
        bool Sample,
        string OutputDirectory,
        string? LocalPackagesFeed);

    internal static class ProjectScaffolder
    {
        public static readonly string[] Databases = ["postgres", "sqlserver", "sqlite", "none"];

        public static string Scaffold(ScaffoldOptions options)
        {
            if (!Naming.IsValidProjectName(options.Name))
                throw new ArgumentException($"'{options.Name}' is not a valid project name. Use letters, digits, dots and underscores, starting with a letter.");

            if (!Databases.Contains(options.Database))
                throw new ArgumentException($"'{options.Database}' is not a supported database. Use one of: {string.Join(", ", Databases)}.");

            var root = Path.Combine(Path.GetFullPath(options.OutputDirectory), options.Name);

            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
                throw new InvalidOperationException($"Directory {root} already exists and is not empty.");

            var manifest = new TrussManifest
            {
                Name = options.Name,
                TrussVersion = TrussVersionInfo.Current(),
                Database = options.Database,
                Docker = options.Docker,
                Sample = options.Sample
            };

            var usesEf = manifest.UsesEntityFramework;

            Directory.CreateDirectory(root);

            Write(root, ".gitignore", ProjectTemplates.GitIgnore, manifest);
            Write(root, "Directory.Build.props", ProjectTemplates.DirectoryBuildProps, manifest);
            Write(root, $"{options.Name}.slnx", usesEf ? ProjectTemplates.SolutionWithInfrastructure : ProjectTemplates.SolutionWithoutInfrastructure, manifest);

            Write(root, Path.Combine(manifest.DomainProject, $"{options.Name}.Domain.csproj"), ProjectTemplates.DomainCsproj, manifest);
            Write(root, Path.Combine(manifest.ApplicationProject, $"{options.Name}.Application.csproj"), ProjectTemplates.ApplicationCsproj, manifest);
            Write(root, Path.Combine(manifest.ApplicationProject, "ApplicationAssemblyMarker.cs"), ProjectTemplates.ApplicationAssemblyMarker, manifest);

            if (usesEf)
            {
                Write(root, Path.Combine(manifest.InfrastructureProject, $"{options.Name}.Infrastructure.csproj"), ProjectTemplates.InfrastructureCsproj, manifest);
                Write(root, Path.Combine(manifest.InfrastructureProject, "AppDbContext.cs"), options.Sample ? ProjectTemplates.AppDbContextSample : ProjectTemplates.AppDbContextEmpty, manifest);
                Write(root, Path.Combine(manifest.ApiProject, $"{options.Name}.Api.csproj"), ProjectTemplates.ApiCsprojWithInfrastructure, manifest);
                Write(root, Path.Combine(manifest.ApiProject, "appsettings.json"), ProjectTemplates.AppSettings, manifest);
            }
            else
            {
                Write(root, Path.Combine(manifest.ApiProject, $"{options.Name}.Api.csproj"), ProjectTemplates.ApiCsprojWithoutInfrastructure, manifest);
                Write(root, Path.Combine(manifest.ApiProject, "appsettings.json"), ProjectTemplates.AppSettingsWithoutDatabase, manifest);
            }

            Write(root, Path.Combine(manifest.ApiProject, "Program.cs"), BuildProgramTemplate(manifest), manifest);
            Write(root, Path.Combine(manifest.ApiProject, "Properties", "launchSettings.json"), ProjectTemplates.LaunchSettings, manifest);

            if (options.Sample)
                WriteSample(root, manifest);

            if (options.LocalPackagesFeed is { } feed)
            {
                Write(root, "NuGet.config", ProjectTemplates.NuGetConfig.Replace("__LOCAL_FEED__", Path.GetFullPath(feed)), manifest);
            }

            ComposeGenerator.Write(manifest, root);
            AgentsGenerator.Write(manifest, root);
            manifest.Save(root);

            return root;
        }

        private static string BuildProgramTemplate(TrussManifest manifest)
        {
            if (!manifest.UsesEntityFramework)
                return ProjectTemplates.ProgramWithoutInfrastructure;

            var template = ProjectTemplates.ProgramWithInfrastructure;

            if (manifest.Sample)
            {
                template = template
                    .Replace(
                        "using __NAME__.Application;",
                        "using __NAME__.Application;\nusing __NAME__.Application.Catalog;")
                    .Replace(
                        "builder.Services.AddTrussEntityFramework<AppDbContext>();",
                        "builder.Services.AddTrussEntityFramework<AppDbContext>();\n\nbuilder.Services.AddInfrastructure();")
                    .Replace(
                        "app.MapGet(\"/\", () => \"__NAME__ is running.\");",
                        SampleTemplates.SampleEndpoints);
            }

            return template;
        }

        private static void WriteSample(string root, TrussManifest manifest)
        {
            var domain = Path.Combine(manifest.DomainProject, "Catalog");
            var application = Path.Combine(manifest.ApplicationProject, "Catalog");
            var infrastructure = Path.Combine(manifest.InfrastructureProject, "Catalog");

            Write(root, Path.Combine(domain, "ProductId.cs"), SampleTemplates.ProductId, manifest);
            Write(root, Path.Combine(domain, "Product.cs"), SampleTemplates.Product, manifest);
            Write(root, Path.Combine(domain, "ProductCreated.cs"), SampleTemplates.ProductCreated, manifest);
            Write(root, Path.Combine(domain, "ProductPriceMustBePositive.cs"), SampleTemplates.ProductPriceMustBePositive, manifest);

            Write(root, Path.Combine(application, "IProductRepository.cs"), SampleTemplates.ProductRepository, manifest);
            Write(root, Path.Combine(application, "CreateProduct.cs"), SampleTemplates.CreateProduct, manifest);
            Write(root, Path.Combine(application, "CreateProductHandler.cs"), SampleTemplates.CreateProductHandler, manifest);
            Write(root, Path.Combine(application, "CreateProductValidator.cs"), SampleTemplates.CreateProductValidator, manifest);
            Write(root, Path.Combine(application, "GetProductById.cs"), SampleTemplates.GetProductById, manifest);
            Write(root, Path.Combine(application, "GetProductByIdHandler.cs"), SampleTemplates.GetProductByIdHandler, manifest);

            Write(root, Path.Combine(infrastructure, "ProductConfiguration.cs"), SampleTemplates.ProductConfiguration, manifest);
            Write(root, Path.Combine(infrastructure, "EfProductRepository.cs"), SampleTemplates.EfProductRepository, manifest);
            Write(root, Path.Combine(manifest.InfrastructureProject, "InfrastructureModule.cs"), SampleTemplates.InfrastructureModule, manifest);
        }

        private static void Write(string root, string relativePath, string template, TrussManifest manifest)
        {
            var path = Path.Combine(root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Render(template, manifest) + Environment.NewLine);
        }

        private static string Render(string template, TrussManifest manifest)
        {
            return template
                .Replace("__NAME__", manifest.Name)
                .Replace("__TRUSS_VERSION__", manifest.TrussVersion)
                .Replace("__EF_PROVIDER_PACKAGE__", ProviderPackage(manifest.Database))
                .Replace("__EF_PROVIDER_METHOD__", ProviderMethod(manifest.Database))
                .Replace("__CONNECTION_STRING__", ConnectionString(manifest));
        }

        private static string ProviderPackage(string database) => database switch
        {
            "postgres" => "Npgsql.EntityFrameworkCore.PostgreSQL",
            "sqlserver" => "Microsoft.EntityFrameworkCore.SqlServer",
            "sqlite" => "Microsoft.EntityFrameworkCore.Sqlite",
            _ => string.Empty
        };

        private static string ProviderMethod(string database) => database switch
        {
            "postgres" => "UseNpgsql",
            "sqlserver" => "UseSqlServer",
            "sqlite" => "UseSqlite",
            _ => string.Empty
        };

        private static string ConnectionString(TrussManifest manifest) => manifest.Database switch
        {
            "postgres" => $"Host=localhost;Port=5432;Database={manifest.Name.ToLowerInvariant()};Username=postgres;Password=truss",
            "sqlserver" => $"Server=localhost,1433;Database={manifest.Name};User Id=sa;Password=Truss!Passw0rd;TrustServerCertificate=true",
            "sqlite" => $"Data Source={manifest.Name.ToLowerInvariant()}.db",
            _ => string.Empty
        };
    }
}
