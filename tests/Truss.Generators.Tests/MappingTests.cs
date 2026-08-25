using Truss.Mapping.Tests.Fakes;
using Xunit;

namespace Truss.Mapping.Tests
{
    public class MappingTests
    {
        private static Product SampleProduct()
        {
            return new Product(
                new ProductId(Guid.NewGuid()),
                "Steel beam",
                149.90m,
                [new OrderLine("BEAM-1", 2), new OrderLine("BOLT-9", 40)]);
        }

        [Fact]
        public void Map_UnwrapsTypedId_AndCopiesProperties()
        {
            var product = SampleProduct();

            var dto = CatalogMappings.ToDto(product);

            Assert.Equal(product.Id.Value, dto.Id);
            Assert.Equal("Steel beam", dto.Name);
            Assert.Equal(149.90m, dto.Price);
        }

        [Fact]
        public void Map_UsesNestedMapper_ForCollections()
        {
            var product = SampleProduct();

            var dto = CatalogMappings.ToDto(product);

            Assert.Equal(2, dto.Lines.Count);
            Assert.Equal("BEAM-1", dto.Lines[0].Sku);
            Assert.Equal(40, dto.Lines[1].Quantity);
        }

        [Fact]
        public void Map_UsesCustomMethod_ForComputedMember()
        {
            var product = SampleProduct();

            var summary = CatalogMappings.ToSummary(product);

            Assert.Equal(product.Id.Value, summary.Id);
            Assert.Equal(2, summary.LineCount);
        }
    }
}
