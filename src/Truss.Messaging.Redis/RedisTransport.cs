using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Truss.Messaging.Redis
{
    /// <summary>
    /// Redis transport. Publishing appends the envelope to a Redis Stream;
    /// consumers read it through a consumer group, so delivery survives restarts.
    /// </summary>
    public sealed class RedisTransport : IMessageTransport, IAsyncDisposable
    {
        private readonly TrussRedisTransportOptions _options;
        private readonly Lazy<Task<ConnectionMultiplexer>> _connection;

        /// <summary>
        /// Initializes the transport with its options.
        /// </summary>
        /// <param name="options">The transport options.</param>
        public RedisTransport(IOptions<TrussRedisTransportOptions> options)
        {
            _options = options.Value;

            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            {
                throw new InvalidOperationException(
                    "The Redis transport requires a connection string. Set TrussRedisTransportOptions.ConnectionString in code or bind it from configuration."
                );
            }

            _connection = new Lazy<Task<ConnectionMultiplexer>>(
                () => ConnectionMultiplexer.ConnectAsync(_options.ConnectionString));
        }

        internal async Task<IDatabase> GetDatabase()
        {
            var connection = await _connection.Value;
            return connection.GetDatabase();
        }

        /// <inheritdoc />
        public async Task Publish(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            var database = await GetDatabase();

            await database.StreamAddAsync(
                _options.StreamKey,
                RedisEnvelope.ToFields(envelope),
                maxLength: _options.MaxStreamLength,
                useApproximateMaxLength: true);
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_connection.IsValueCreated)
            {
                var connection = await _connection.Value;
                await connection.DisposeAsync();
            }
        }
    }
}
