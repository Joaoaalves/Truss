namespace Truss.Cli.Templates
{
    internal static class SampleTemplates
    {
        public const string ProductId = """
            using Truss.Domain;

            namespace __NAME__.Domain.Catalog
            {
                public sealed record ProductId(Guid Value) : TypedId<Guid>(Value);
            }
            """;

        public const string ProductCreated = """
            using Truss.Domain;

            namespace __NAME__.Domain.Catalog
            {
                public sealed record ProductCreated(ProductId ProductId) : DomainEvent;
            }
            """;

        public const string ProductPriceMustBePositive = """
            using Truss.Domain;

            namespace __NAME__.Domain.Catalog
            {
                public class ProductPriceMustBePositive(decimal price) : IBusinessRule
                {
                    public bool IsBroken() => price <= 0;

                    public string Message => "The product price must be positive.";
                }
            }
            """;

        public const string Product = """
            using Truss.Domain;

            namespace __NAME__.Domain.Catalog
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
            using __NAME__.Domain.Catalog;

            namespace __NAME__.Application.Catalog
            {
                public interface IProductRepository
                {
                    void Add(Product product);

                    Task<Product?> GetById(ProductId id, CancellationToken cancellationToken = default);
                }
            }
            """;

        public const string CreateProduct = """
            using Truss.Application;

            namespace __NAME__.Application.Catalog
            {
                public sealed record CreateProduct(string Name, decimal Price) : ICommand<Guid>;
            }
            """;

        public const string CreateProductHandler = """
            using __NAME__.Domain.Catalog;
            using Truss.Application;

            namespace __NAME__.Application.Catalog
            {
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
            using FluentValidation;

            namespace __NAME__.Application.Catalog
            {
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
            using Truss.Application;

            namespace __NAME__.Application.Catalog
            {
                public sealed record ProductDto(Guid Id, string Name, decimal Price);

                public sealed record GetProductById(Guid Id) : IQuery<ProductDto?>;
            }
            """;

        public const string GetProductByIdHandler = """
            using __NAME__.Domain.Catalog;
            using Truss.Application;

            namespace __NAME__.Application.Catalog
            {
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
            using __NAME__.Domain.Catalog;
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
            using __NAME__.Application.Catalog;
            using __NAME__.Domain.Catalog;
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

        public const string InfrastructureModule = """
            using __NAME__.Application.Catalog;
            using __NAME__.Infrastructure.Catalog;
            using Microsoft.Extensions.DependencyInjection;

            namespace __NAME__.Infrastructure
            {
                public static class InfrastructureModule
                {
                    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
                    {
                        services.AddScoped<IProductRepository, EfProductRepository>();
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
            using __NAME__.Application.Catalog;
            """;
    }
}
