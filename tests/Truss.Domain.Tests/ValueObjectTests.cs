using Truss.Domain.Tests.Fakes;
using Xunit;

namespace Truss.Domain.Tests
{
    public class ValueObjectTests
    {
        [Fact]
        public void ValueObjects_WithSameComponents_AreEqual()
        {
            var first = new FakeAddress("Main St", "Springfield");
            var second = new FakeAddress("Main St", "Springfield");

            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void ValueObjects_WithDifferentComponents_AreNotEqual()
        {
            var first = new FakeAddress("Main St", "Springfield");
            var second = new FakeAddress("Main St", "Shelbyville");

            Assert.NotEqual(first, second);
            Assert.True(first != second);
        }

        [Fact]
        public void ValueObjects_OfDifferentTypes_AreNotEqual()
        {
            var first = new FakeAddress("Main St", "Springfield");
            var second = new OtherAddress("Main St", "Springfield");

            Assert.False(first.Equals(second));
        }

        [Fact]
        public void ValueObject_ComparedToNull_IsNotEqual()
        {
            var address = new FakeAddress("Main St", "Springfield");

            Assert.False(address.Equals(null));
            Assert.False(address == null);
            Assert.True(address != null);
        }
    }
}
