namespace Truss.Cli.Templates
{
    /// <summary>
    /// The sample Catalog bounded context. It is laid out exactly like the
    /// generators lay their output out: the aggregate owns a folder with its
    /// value objects, events and rules, and each command or query owns a folder
    /// with its handler and validator.
    /// </summary>
    internal static class SampleTemplates
    {
        public const string ProductId = """
            using Truss.Domain;

            namespace __NAME__.Domain.Catalog.Product.ValueObjects
            {
                public sealed record ProductId(Guid Value) : TypedId<Guid>(Value);
            }
            """;

        public const string ProductCreated = """
            using __NAME__.Domain.Catalog.Product.ValueObjects;
            using Truss.Domain;

            namespace __NAME__.Domain.Catalog.Product.Events
            {
                public sealed record ProductCreated(ProductId ProductId) : DomainEvent;
            }
            """;

        public const string ProductPriceMustBePositive = """
            using Truss.Domain;

            namespace __NAME__.Domain.Catalog.Product.Rules
            {
                public class ProductPriceMustBePositive(decimal price) : IBusinessRule
                {
                    public bool IsBroken() => price <= 0;

                    public string Message => "The product price must be positive.";
                }
            }
            """;

        public const string Product = """
            using __NAME__.Domain.Catalog.Product.Events;
            using __NAME__.Domain.Catalog.Product.Rules;
            using __NAME__.Domain.Catalog.Product.ValueObjects;
            using Truss.Domain;

            namespace __NAME__.Domain.Catalog.Product
            {
                public class Product : AggregateRoot<ProductId>
                {
                    private Product()
                    {
                    }

                    private Product(ProductId id, string name, decimal price) : base(id)
                    {
                        Name = name;
                        Price = price;
                    }

                    public string Name { get; private set; } = string.Empty;

                    public decimal Price { get; private set; }

                    public static Product Create(string name, decimal price)
                    {
                        CheckRule(new ProductPriceMustBePositive(price));

                        var product = new Product(new ProductId(Guid.NewGuid()), name, price);
                        product.AddDomainEvent(new ProductCreated(product.Id));
                        return product;
                    }
                }
            }
            """;

        public const string ProductRepository = """
            namespace __NAME__.Application.Catalog.Product
            {
                using __NAME__.Domain.Catalog.Product;
                using __NAME__.Domain.Catalog.Product.ValueObjects;

                public interface IProductRepository
                {
                    void Add(Product product);

                    Task<Product?> GetById(ProductId id, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string ProductDto = """
            namespace __NAME__.Application.Catalog.Product.DTOs
            {
                public sealed record ProductDto(Guid Id, string Name, decimal Price);
            }
            """;

        public const string CreateProduct = """
            namespace __NAME__.Application.Catalog.Product.CreateProduct
            {
                using Truss.Application;

                public sealed record CreateProduct(string Name, decimal Price) : ICommand<Guid>;
            }
            """;

        public const string CreateProductHandler = """
            namespace __NAME__.Application.Catalog.Product.CreateProduct
            {
                using __NAME__.Application.Catalog.Product;
                using __NAME__.Domain.Catalog.Product;
                using Truss.Application;

                public class CreateProductHandler(IProductRepository products) : ICommandHandler<CreateProduct, Guid>
                {
                    public Task<Guid> Handle(CreateProduct command, CancellationToken cancellationToken)
                    {
                        var product = Product.Create(command.Name, command.Price);
                        products.Add(product);
                        return Task.FromResult(product.Id.Value);
                    }
                }
            }
            """;

        public const string CreateProductValidator = """
            namespace __NAME__.Application.Catalog.Product.CreateProduct
            {
                using FluentValidation;

                public class CreateProductValidator : AbstractValidator<CreateProduct>
                {
                    public CreateProductValidator()
                    {
                        RuleFor(command => command.Name).NotEmpty().MaximumLength(200);
                        RuleFor(command => command.Price).GreaterThan(0);
                    }
                }
            }
            """;

        public const string GetProductById = """
            namespace __NAME__.Application.Catalog.Product.GetProductById
            {
                using __NAME__.Application.Catalog.Product.DTOs;
                using Truss.Application;

                public sealed record GetProductById(Guid Id) : IQuery<ProductDto?>;
            }
            """;

        public const string GetProductByIdHandler = """
            namespace __NAME__.Application.Catalog.Product.GetProductById
            {
                using __NAME__.Application.Catalog.Product;
                using __NAME__.Application.Catalog.Product.DTOs;
                using __NAME__.Domain.Catalog.Product.ValueObjects;
                using Truss.Application;

                public class GetProductByIdHandler(IProductRepository products) : IQueryHandler<GetProductById, ProductDto?>
                {
                    public async Task<ProductDto?> Handle(GetProductById query, CancellationToken cancellationToken)
                    {
                        var product = await products.GetById(new ProductId(query.Id), cancellationToken);

                        return product is null
                            ? null
                            : new ProductDto(product.Id.Value, product.Name, product.Price);
                    }
                }
            }
            """;

        public const string ProductConfiguration = """
            using __NAME__.Domain.Catalog.Product;
            using __NAME__.Domain.Catalog.Product.ValueObjects;
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;

            namespace __NAME__.Infrastructure.Catalog
            {
                public class ProductConfiguration : IEntityTypeConfiguration<Product>
                {
                    public void Configure(EntityTypeBuilder<Product> builder)
                    {
                        builder.ToTable("Products");
                        builder.HasKey(product => product.Id);

                        builder.Property(product => product.Id)
                            .HasConversion(id => id.Value, value => new ProductId(value));

                        builder.Property(product => product.Name).HasMaxLength(200).IsRequired();
                        builder.Property(product => product.Price).HasPrecision(18, 2);
                    }
                }
            }
            """;

        public const string EfProductRepository = """
            using __NAME__.Application.Catalog.Product;
            using __NAME__.Domain.Catalog.Product;
            using __NAME__.Domain.Catalog.Product.ValueObjects;
            using Microsoft.EntityFrameworkCore;

            namespace __NAME__.Infrastructure.Catalog
            {
                public class EfProductRepository(AppDbContext context) : IProductRepository
                {
                    public void Add(Product product)
                    {
                        context.Products.Add(product);
                    }

                    public Task<Product?> GetById(ProductId id, CancellationToken cancellationToken = default)
                    {
                        return context.Products.FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
                    }
                }
            }
            """;

        public const string CatalogSeeder = """
            using __NAME__.Domain.Catalog.Product;
            using Microsoft.EntityFrameworkCore;
            using Truss.EntityFrameworkCore;

            namespace __NAME__.Infrastructure.Catalog
            {
                public class CatalogSeeder(AppDbContext context) : ITrussSeeder
                {
                    public async Task Seed(CancellationToken cancellationToken = default)
                    {
                        if (await context.Products.AnyAsync(cancellationToken))
                            return;

                        context.Products.Add(Product.Create("Beam", 10m));
                        context.Products.Add(Product.Create("Column", 25m));

                        await context.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            """;

        public const string InfrastructureModule = """
            using __NAME__.Application.Catalog.Product;
            using __NAME__.Infrastructure.Catalog;
            using Microsoft.Extensions.DependencyInjection;

            namespace __NAME__.Infrastructure
            {
                public static class InfrastructureModule
                {
                    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
                    {
                        services.AddScoped<IProductRepository, EfProductRepository>();
                        services.AddTrussSeeder<CatalogSeeder>();
                        return services;
                    }
                }
            }
            """;

        public const string SampleRegistration = "builder.Services.AddInfrastructure();";

        public const string SampleEndpoints = """
            app.MapCommand<CreateProduct, Guid>("/products", id => $"/products/{id}");
            app.MapQuery<GetProductById, ProductDto?>("/products/{id}");
            """;

        public const string SampleUsings = """
            using __NAME__.Application.Catalog.Product.CreateProduct;
            using __NAME__.Application.Catalog.Product.DTOs;
            using __NAME__.Application.Catalog.Product.GetProductById;
            """;
    }
}
