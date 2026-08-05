using Truss.Domain.Tests.Fakes;
using Xunit;

namespace Truss.Domain.Tests.CommonTests
{
    public class TypedIdTests
    {
        [Fact]
        public void TypedIds_WithSameValue_AreEqual()
        {
            var value = Guid.NewGuid();

            var first = new FakeId(value);
            var second = new FakeId(value);

            Assert.Equal(first, second);
            Assert.True(first == second);
        }

        [Fact]
        public void TypedIds_WithDifferentValues_AreNotEqual()
        {
            var first = new FakeId(Guid.NewGuid());
            var second = new FakeId(Guid.NewGuid());

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void TypedIds_OfDifferentTypes_AreNotEqual()
        {
            var value = Guid.NewGuid();

            var first = new FakeId(value);
            var second = new OtherId(value);

            Assert.False(first.Equals((object)second));
        }

        [Fact]
        public void ToString_ReturnsUnderlyingValue()
        {
            var value = Guid.NewGuid();

            var id = new FakeId(value);

            Assert.Equal(value.ToString(), id.ToString());
        }
    }
}
