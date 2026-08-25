using System.Diagnostics;
using Truss.Messaging.Serialization;
using Truss.Messaging.Transport;

namespace Truss.Messaging.Dispatch
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

            var envelope = _serializer.Serialize(integrationEvent) with { TraceParent = Activity.Current?.Id };

            return _transport.Publish(envelope, cancellationToken);
        }
    }
}
