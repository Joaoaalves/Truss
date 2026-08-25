using System.Threading.Channels;
using Truss.Messaging.Serialization;

namespace Truss.Messaging.Transport
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

        private int _pending;

        internal ChannelReader<IntegrationEventEnvelope> Reader => _channel.Reader;

        internal void MarkDelivered() => Interlocked.Decrement(ref _pending);

        /// <inheritdoc />
        public async Task Publish(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            Interlocked.Increment(ref _pending);
            await _channel.Writer.WriteAsync(envelope, cancellationToken);
        }

        /// <summary>
        /// Completes when every published message has been handled.
        /// Delivery is asynchronous even in-process; test helpers use this
        /// to make it deterministic.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        public async Task WaitForIdle(CancellationToken cancellationToken = default)
        {
            while (Volatile.Read(ref _pending) > 0)
                await Task.Delay(10, cancellationToken);
        }
    }
}
