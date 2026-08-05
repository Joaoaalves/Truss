using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Truss.Messaging.RabbitMq
{
    /// <summary>
    /// RabbitMQ transport. Publishing sends the envelope to a durable quorum queue
    /// with publisher confirms, so a completed publish is on disk at the broker.
    /// </summary>
    public sealed class RabbitMqTransport : IMessageTransport, IAsyncDisposable
    {
        private readonly TrussRabbitMqTransportOptions _options;
        private readonly Lazy<Task<IConnection>> _connection;
        private readonly SemaphoreSlim _publishLock = new(1, 1);
        private IChannel? _publishChannel;

        /// <summary>
        /// Initializes the transport with its options.
        /// </summary>
        /// <param name="options">The transport options.</param>
        public RabbitMqTransport(IOptions<TrussRabbitMqTransportOptions> options)
        {
            _options = options.Value;

            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            {
                throw new InvalidOperationException(
                    "The RabbitMQ transport requires a connection string. Set TrussRabbitMqTransportOptions.ConnectionString in code or bind it from configuration."
                );
            }

            var factory = new ConnectionFactory { Uri = new Uri(_options.ConnectionString) };
            _connection = new Lazy<Task<IConnection>>(() => factory.CreateConnectionAsync());
        }

        internal Task<IConnection> GetConnection()
        {
            return _connection.Value;
        }

        internal TrussRabbitMqTransportOptions Options => _options;

        /// <inheritdoc />
        public async Task Publish(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            await _publishLock.WaitAsync(cancellationToken);

            try
            {
                var channel = await GetPublishChannel(cancellationToken);

                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    MessageId = envelope.MessageId.ToString(),
                    Type = envelope.Name
                };

                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: _options.QueueName,
                    mandatory: false,
                    basicProperties: properties,
                    body: RabbitMqEnvelope.ToBody(envelope),
                    cancellationToken: cancellationToken);
            }
            finally
            {
                _publishLock.Release();
            }
        }

        private async Task<IChannel> GetPublishChannel(CancellationToken cancellationToken)
        {
            if (_publishChannel is { IsOpen: true })
                return _publishChannel;

            if (_publishChannel is not null)
                await _publishChannel.DisposeAsync();

            var connection = await GetConnection();

            _publishChannel = await connection.CreateChannelAsync(
                new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            await RabbitMqTopology.Declare(_publishChannel, _options, cancellationToken);

            return _publishChannel;
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_publishChannel is not null)
                await _publishChannel.DisposeAsync();

            if (_connection.IsValueCreated)
            {
                var connection = await _connection.Value;
                await connection.DisposeAsync();
            }

            _publishLock.Dispose();
        }
    }
}
