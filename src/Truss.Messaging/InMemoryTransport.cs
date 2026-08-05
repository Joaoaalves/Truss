using System.Threading.Channels;

namespace Truss.Messaging
{
    /// <summary>
    /// In-process transport backed by a channel.
    /// Intended for development, tests and modular monoliths that do not need a broker.
    /// A failed handler is logged and the message is dropped; use a durable transport in production.
    /// </summary>
    public sealed class InMemoryTransport : IMessageTransport
    {
        private readonly Channel<IntegrationEventEnvelope> _channel =
            Channel.CreateUnbounded<IntegrationEventEnvelope>();

        internal ChannelReader<IntegrationEventEnvelope> Reader => _channel.Reader;

        /// <inheritdoc />
        public async Task Publish(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            await _channel.Writer.WriteAsync(envelope, cancellationToken);
        }
    }
}
