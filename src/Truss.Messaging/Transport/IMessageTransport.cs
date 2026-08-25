using Truss.Messaging.Dispatch;
using Truss.Messaging.Serialization;

namespace Truss.Messaging.Transport
{
    /// <summary>
    /// The seam between Truss and the message broker.
    /// Each transport package (in-memory, Postgres, Redis, RabbitMQ) implements this contract
    /// and hosts its own consumer that feeds received envelopes into the
    /// <see cref="IIntegrationEventDispatcher"/>.
    /// </summary>
    public interface IMessageTransport
    {
        /// <summary>
        /// Publishes an envelope to the underlying broker.
        /// </summary>
        /// <param name="envelope">The envelope to publish.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Publish(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default);
    }
}
