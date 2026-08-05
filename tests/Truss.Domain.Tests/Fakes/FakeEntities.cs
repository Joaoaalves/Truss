using Truss.Domain;

namespace Truss.Domain.Tests.Fakes
{
    public class FakeEntity : Entity<Guid>
    {
        public FakeEntity()
        {
        }

        public FakeEntity(Guid id) : base(id)
        {
        }

        public void Raise(IDomainEvent domainEvent) => AddDomainEvent(domainEvent);

        public void EnforceRule(IBusinessRule rule) => CheckRule(rule);
    }

    public class OtherFakeEntity : Entity<Guid>
    {
        public OtherFakeEntity(Guid id) : base(id)
        {
        }
    }

    public class FakeAggregate : AggregateRoot<Guid>
    {
        public FakeAggregate(Guid id) : base(id)
        {
        }
    }
}
