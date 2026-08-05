namespace Truss.Messaging
{
    /// <summary>
    /// Publishes integration events through the outbox.
    /// The event is stored in the same transaction as the current command and delivered
    /// to the transport by the outbox processor after the commit.
    /// </summary>
    public class OutboxIntegrationEventPublisher(
        IOutboxStore store,
        IIntegrationEventSerializer serializer) : IIntegrationEventPublisher
    {
        private readonly IOutboxStore _store = store;
        private readonly IIntegrationEventSerializer _serializer = serializer;

        /// <inheritdoc />
        public Task Publish(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(integrationEvent);

            var envelope = _serializer.Serialize(integrationEvent);

            var message = new OutboxMessage(
                envelope.MessageId,
                envelope.Name,
                envelope.Version,
                envelope.Payload,
                envelope.OccurredOn);

            return _store.Add(message, cancellationToken);
        }
    }
}
