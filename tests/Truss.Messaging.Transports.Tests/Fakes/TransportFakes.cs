namespace Truss.Messaging.Transports.Tests.Fakes
{
    [IntegrationEventName("transport-test.item-created")]
    public sealed record ItemCreated(Guid ItemId) : IntegrationEvent;

    [IntegrationEventName("transport-test.throwing")]
    public sealed record ThrowingEvent : IntegrationEvent;

    public class ReceivedEvents
    {
        private readonly Lock _lock = new();
        private readonly List<IIntegrationEvent> _events = [];

        public void Add(IIntegrationEvent integrationEvent)
        {
            lock (_lock)
            {
                _events.Add(integrationEvent);
            }
        }

        public IReadOnlyList<IIntegrationEvent> Snapshot()
        {
            lock (_lock)
            {
                return [.. _events];
            }
        }
    }

    public class ItemCreatedHandler(ReceivedEvents received) : IIntegrationEventHandler<ItemCreated>
    {
        public Task Handle(ItemCreated integrationEvent, CancellationToken cancellationToken)
        {
            received.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    public class ThrowingEventHandler : IIntegrationEventHandler<ThrowingEvent>
    {
        public Task Handle(ThrowingEvent integrationEvent, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Handler failed.");
        }
    }
}
