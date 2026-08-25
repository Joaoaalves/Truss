using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Truss.Messaging.Dispatch;
using Truss.Messaging.Serialization;

namespace Truss.Messaging.Postgres
{
    internal sealed class PostgresConsumer(
        IIntegrationEventDispatcher dispatcher,
        IOptions<TrussPostgresTransportOptions> options,
        ILogger<PostgresConsumer> logger) : BackgroundService
    {
        private const string FetchSql = $"""
            SELECT sequence, message_id, name, version, occurred_on, payload, attempts, trace_parent
            FROM {PostgresSchema.MessagesTable}
            WHERE visible_on <= now()
            ORDER BY sequence
            LIMIT @batchSize
            FOR UPDATE SKIP LOCKED
            """;

        private const string DeleteSql =
            $"DELETE FROM {PostgresSchema.MessagesTable} WHERE sequence = @sequence";

        private const string RetrySql = $"""
            UPDATE {PostgresSchema.MessagesTable}
            SET attempts = @attempts, visible_on = now() + @delay
            WHERE sequence = @sequence
            """;

        private const string DeadLetterSql = $"""
            INSERT INTO {PostgresSchema.DeadLetterTable}
                (sequence, message_id, name, version, occurred_on, payload, attempts, error, failed_on, trace_parent)
            SELECT sequence, message_id, name, version, occurred_on, payload, @attempts, @error, now(), trace_parent
            FROM {PostgresSchema.MessagesTable}
            WHERE sequence = @sequence;
            DELETE FROM {PostgresSchema.MessagesTable} WHERE sequence = @sequence;
            """;

        private readonly TrussPostgresTransportOptions _options = options.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.EnableConsumer)
                return;

            PostgresSchema.ValidateChannel(_options.Channel);
            await PostgresSchema.EnsureCreated(_options, stoppingToken);

            await using var listenConnection = new NpgsqlConnection(_options.ConnectionString);
            await listenConnection.OpenAsync(stoppingToken);

            await using (var listen = new NpgsqlCommand($"LISTEN {_options.Channel}", listenConnection))
            {
                await listen.ExecuteNonQueryAsync(stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var processed = await ProcessBatchAsync(stoppingToken);

                    if (processed == 0)
                        await listenConnection.WaitAsync(_options.PollingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Postgres consumer iteration failed.");

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
        {
            await using var connection = new NpgsqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            var messages = new List<(long Sequence, int Attempts, IntegrationEventEnvelope Envelope)>();

            await using (var fetch = new NpgsqlCommand(FetchSql, connection, transaction))
            {
                fetch.Parameters.AddWithValue("batchSize", _options.BatchSize);

                await using var reader = await fetch.ExecuteReaderAsync(cancellationToken);

                while (await reader.ReadAsync(cancellationToken))
                {
                    messages.Add((
                        reader.GetInt64(0),
                        reader.GetInt32(6),
                        new IntegrationEventEnvelope(
                            reader.GetGuid(1),
                            reader.GetString(2),
                            reader.GetInt32(3),
                            reader.GetFieldValue<DateTimeOffset>(4),
                            reader.GetString(5),
                            reader.IsDBNull(7) ? null : reader.GetString(7))));
                }
            }

            foreach (var (sequence, attempts, envelope) in messages)
            {
                try
                {
                    await dispatcher.Dispatch(envelope, cancellationToken);

                    await using var delete = new NpgsqlCommand(DeleteSql, connection, transaction);
                    delete.Parameters.AddWithValue("sequence", sequence);
                    await delete.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await RegisterFailureAsync(connection, transaction, sequence, attempts + 1, envelope, exception, cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);

            return messages.Count;
        }

        private async Task RegisterFailureAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            long sequence,
            int attempts,
            IntegrationEventEnvelope envelope,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (attempts >= _options.MaxAttempts)
            {
                await using var dead = new NpgsqlCommand(DeadLetterSql, connection, transaction);
                dead.Parameters.AddWithValue("sequence", sequence);
                dead.Parameters.AddWithValue("attempts", attempts);
                dead.Parameters.AddWithValue("error", exception.Message);
                await dead.ExecuteNonQueryAsync(cancellationToken);

                logger.LogError(exception, "Message {MessageId} ({Name} v{Version}) was dead-lettered after {Attempts} attempts.", envelope.MessageId, envelope.Name, envelope.Version, attempts);
                return;
            }

            var delay = RetryDelayFor(attempts);

            await using var retry = new NpgsqlCommand(RetrySql, connection, transaction);
            retry.Parameters.AddWithValue("sequence", sequence);
            retry.Parameters.AddWithValue("attempts", attempts);
            retry.Parameters.AddWithValue("delay", delay);
            await retry.ExecuteNonQueryAsync(cancellationToken);

            logger.LogWarning(exception, "Message {MessageId} ({Name} v{Version}) failed attempt {Attempts}; retrying in {Delay}.", envelope.MessageId, envelope.Name, envelope.Version, attempts, delay);
        }

        private TimeSpan RetryDelayFor(int attempts)
        {
            var delay = _options.RetryBaseDelay * Math.Pow(2, attempts - 1);
            return delay > _options.RetryMaxDelay ? _options.RetryMaxDelay : delay;
        }
    }
}
