using Truss.Domain;

namespace Truss.Persistence.EntityFrameworkCore.Tests.Fakes
{
    public sealed record OrderPlaced(Guid OrderId) : DomainEvent;

    public sealed record OrderArchived(Guid OrderId) : DomainEvent;

    public class Order : AggregateRoot<Guid>
    {
        private Order()
        {
        }

        public Order(Guid id) : base(id)
        {
        }

        public string Status { get; private set; } = "Draft";

        public void Place()
        {
            Status = "Placed";
            AddDomainEvent(new OrderPlaced(Id));
        }

        public void Archive()
        {
            Status = "Archived";
            AddDomainEvent(new OrderArchived(Id));
        }
    }
}
