using Truss.Domain;
using Truss.Mapping;

namespace Truss.Mapping.Tests.Fakes
{
    public sealed record ProductId(Guid Value) : TypedId<Guid>(Value);

    public sealed record OrderLine(string Sku, int Quantity);

    public class Product : AggregateRoot<ProductId>
    {
        public Product(ProductId id, string name, decimal price, List<OrderLine> lines) : base(id)
        {
            Name = name;
            Price = price;
            Lines = lines;
        }

        public string Name { get; }

        public decimal Price { get; }

        public List<OrderLine> Lines { get; }
    }

    public sealed record OrderLineDto(string Sku, int Quantity);

    public sealed record ProductDto(Guid Id, string Name, decimal Price, IReadOnlyList<OrderLineDto> Lines);

    public sealed record ProductSummaryDto(Guid Id, string Name)
    {
        public int LineCount { get; init; }
    }

    [Mapper]
    public static partial class CatalogMappings
    {
        public static partial ProductDto ToDto(Product product);

        public static partial OrderLineDto ToDto(OrderLine line);

        public static partial ProductSummaryDto ToSummary(Product product);

        public static int LineCount(Product product) => product.Lines.Count;
    }
}
