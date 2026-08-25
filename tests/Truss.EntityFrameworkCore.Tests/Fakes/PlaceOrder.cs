using Truss.Application;

namespace Truss.EntityFrameworkCore.Tests.Fakes
{
    public sealed record PlaceOrderCommand(Guid OrderId) : ICommand;

    public class PlaceOrderCommandHandler(TestDbContext context) : ICommandHandler<PlaceOrderCommand>
    {
        public Task<Unit> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
        {
            var order = new Order(request.OrderId);
            order.Place();

            context.Orders.Add(order);

            return Task.FromResult(Unit.Value);
        }
    }

    public class EventRecorder
    {
        public List<Guid> PlacedOrders { get; } = [];
    }

    public class OrderPlacedHandler(EventRecorder recorder) : IDomainEventHandler<OrderPlaced>
    {
        public Task Handle(OrderPlaced domainEvent, CancellationToken cancellationToken)
        {
            recorder.PlacedOrders.Add(domainEvent.OrderId);
            return Task.CompletedTask;
        }
    }
}
