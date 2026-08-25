using Microsoft.Extensions.Options;
using Npgsql;
using Truss.Messaging.Serialization;
using Truss.Messaging.Transport;

namespace Truss.Messaging.Postgres
{
    /// <summary>
    /// Postgres transport.
    /// Publishing inserts the envelope into a durable queue table and raises a NOTIFY
    /// so consumers wake immediately; delivery does not depend on the notification.
    /// </summary>
    public sealed class PostgresTransport : IMessageTransport
    {
        private const string InsertSql = $"""
            INSERT INTO {PostgresSchema.MessagesTable} (message_id, name, version, occurred_on, payload, trace_parent)
            VALUES (@messageId, @name, @version, @occurredOn, @payload, @traceParent);
            SELECT pg_notify(@channel, '');
            """;

        private readonly TrussPostgresTransportOptions _options;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private bool _schemaReady;

        /// <summary>
        /// Initializes the transport with its options.
        /// </summary>
        /// <param name="options">The transport options.</param>
        public PostgresTransport(IOptions<TrussPostgresTransportOptions> options)
        {
            _options = options.Value;

            if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            {
                throw new InvalidOperationException(
                    "The Postgres transport requires a connection string. Set TrussPostgresTransportOptions.ConnectionString in code or bind it from configuration."
                );
            }

            PostgresSchema.ValidateChannel(_options.Channel);
        }

        /// <inheritdoc />
        public async Task Publish(IntegrationEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            await EnsureSchema(cancellationToken);

            await using var connection = new NpgsqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new NpgsqlCommand(InsertSql, connection);
            command.Parameters.AddWithValue("messageId", envelope.MessageId);
            command.Parameters.AddWithValue("name", envelope.Name);
            command.Parameters.AddWithValue("version", envelope.Version);
            command.Parameters.AddWithValue("occurredOn", envelope.OccurredOn);
            command.Parameters.AddWithValue("payload", envelope.Payload);
            command.Parameters.AddWithValue("traceParent", (object?)envelope.TraceParent ?? DBNull.Value);
            command.Parameters.AddWithValue("channel", _options.Channel);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task EnsureSchema(CancellationToken cancellationToken)
        {
            if (_schemaReady)
                return;

            await _schemaLock.WaitAsync(cancellationToken);

            try
            {
                if (_schemaReady)
                    return;

                await PostgresSchema.EnsureCreated(_options, cancellationToken);
                _schemaReady = true;
            }
            finally
            {
                _schemaLock.Release();
            }
        }
    }
}
