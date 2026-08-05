using Truss.Domain.Rules;
using Truss.Domain.Tests.Fakes;
using Xunit;

namespace Truss.Domain.Tests.CommonTests
{
    public class EntityTests
    {
        [Fact]
        public void Entities_WithSameId_AreEqual()
        {
            var id = Guid.NewGuid();

            var first = new FakeEntity(id);
            var second = new FakeEntity(id);

            Assert.Equal(first, second);
            Assert.True(first == second);
            Assert.Equal(first.GetHashCode(), second.GetHashCode());
        }

        [Fact]
        public void Entities_WithDifferentIds_AreNotEqual()
        {
            var first = new FakeEntity(Guid.NewGuid());
            var second = new FakeEntity(Guid.NewGuid());

            Assert.NotEqual(first, second);
            Assert.True(first != second);
        }

        [Fact]
        public void TransientEntities_AreNotEqual()
        {
            var first = new FakeEntity();
            var second = new FakeEntity();

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Entities_OfDifferentTypes_AreNotEqual()
        {
            var id = Guid.NewGuid();

            var first = new FakeEntity(id);
            var second = new OtherFakeEntity(id);

            Assert.False(first.Equals(second));
        }

        [Fact]
        public void DomainEvents_IsEmptyByDefault()
        {
            var entity = new FakeEntity(Guid.NewGuid());

            Assert.Empty(entity.DomainEvents);
        }

        [Fact]
        public void AddDomainEvent_AddsEventToCollection()
        {
            var entity = new FakeEntity(Guid.NewGuid());
            var domainEvent = new FakeDomainEvent();

            entity.Raise(domainEvent);

            Assert.Single(entity.DomainEvents);
            Assert.Contains(domainEvent, entity.DomainEvents);
        }

        [Fact]
        public void ClearDomainEvents_RemovesAllEvents()
        {
            var entity = new FakeEntity(Guid.NewGuid());
            entity.Raise(new FakeDomainEvent());

            entity.ClearDomainEvents();

            Assert.Empty(entity.DomainEvents);
        }

        [Fact]
        public void CheckRule_WhenRuleIsBroken_Throws()
        {
            var entity = new FakeEntity(Guid.NewGuid());
            var rule = new FakeBusinessRule(isBroken: true);

            var exception = Assert.Throws<BusinessRuleValidationException>(() => entity.EnforceRule(rule));

            Assert.Same(rule, exception.BrokenRule);
        }

        [Fact]
        public void CheckRule_WhenRuleIsNotBroken_DoesNotThrow()
        {
            var entity = new FakeEntity(Guid.NewGuid());

            entity.EnforceRule(new FakeBusinessRule(isBroken: false));
        }
    }
}
