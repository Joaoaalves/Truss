namespace Truss.Messaging
{
    /// <summary>
    /// Publishes integration events straight to the transport, without an outbox.
    /// Delivery is best effort: an event published inside a command is sent even if
    /// the command later fails to commit. Configure the outbox for transactional publishing.
    /// </summary>
    public class DirectIntegrationEventPublisher(
        IMessageTransport transport,
        IIntegrationEventSerializer serializer) : IIntegrationEventPublisher
    {
        private readonly IMessageTransport _transport = transport;
        private readonly IIntegrationEventSerializer _serializer = serializer;

        /// <inheritdoc />
        public Task Publish(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(integrationEvent);

            return _transport.Publish(_serializer.Serialize(integrationEvent), cancellationToken);
        }
    }
}
