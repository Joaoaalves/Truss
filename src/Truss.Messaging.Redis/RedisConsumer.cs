using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Truss.Messaging.Dispatch;

namespace Truss.Messaging.Redis
{
    internal sealed class RedisConsumer(
        RedisTransport transport,
        IIntegrationEventDispatcher dispatcher,
        IOptions<TrussRedisTransportOptions> options,
        ILogger<RedisConsumer> logger) : BackgroundService
    {
        private readonly TrussRedisTransportOptions _options = options.Value;
        private string _consumerName = string.Empty;

        private string DeadLetterKey => _options.StreamKey + ":dead";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.EnableConsumer)
                return;

            _consumerName = _options.ConsumerName ?? $"{Environment.MachineName}-{Guid.NewGuid():N}";

            var database = await transport.GetDatabase();
            await EnsureConsumerGroup(database);

            while (!stoppingToken.IsCancellationRequested)
            {
                var processed = 0;

                try
                {
                    processed = await ReadNewMessages(database, stoppingToken);
                    processed += await ProcessPendingMessages(database, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Redis consumer iteration failed.");
                }

                if (processed == 0)
                {
                    try
                    {
                        await Task.Delay(_options.PollingInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task EnsureConsumerGroup(IDatabase database)
        {
            try
            {
                await database.StreamCreateConsumerGroupAsync(
                    _options.StreamKey, _options.ConsumerGroup, StreamPosition.Beginning, createStream: true);
            }
            catch (RedisServerException exception) when (exception.Message.Contains("BUSYGROUP"))
            {
            }
        }

        private async Task<int> ReadNewMessages(IDatabase database, CancellationToken cancellationToken)
        {
            var entries = await database.StreamReadGroupAsync(
                _options.StreamKey, _options.ConsumerGroup, _consumerName,
                StreamPosition.NewMessages, _options.BatchSize);

            foreach (var entry in entries)
            {
                if (await TryDispatch(database, entry, cancellationToken))
                    await database.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id);
            }

            return entries.Length;
        }

        private async Task<int> ProcessPendingMessages(IDatabase database, CancellationToken cancellationToken)
        {
            var pending = await database.StreamPendingMessagesAsync(
                _options.StreamKey, _options.ConsumerGroup, _options.BatchSize, RedisValue.Null);

            var handled = 0;

            foreach (var info in pending)
            {
                if (info.IdleTimeInMilliseconds < _options.ReclaimIdleAfter.TotalMilliseconds)
                    continue;

                if (info.DeliveryCount >= _options.MaxAttempts)
                {
                    await DeadLetter(database, info);
                    handled++;
                    continue;
                }

                var claimed = await database.StreamClaimAsync(
                    _options.StreamKey, _options.ConsumerGroup, _consumerName,
                    (long)_options.ReclaimIdleAfter.TotalMilliseconds, [info.MessageId]);

                foreach (var entry in claimed)
                {
                    handled++;

                    if (await TryDispatch(database, entry, cancellationToken))
                        await database.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id);
                }
            }

            return handled;
        }

        private async Task<bool> TryDispatch(IDatabase database, StreamEntry entry, CancellationToken cancellationToken)
        {
            var envelope = RedisEnvelope.FromEntry(entry);

            if (envelope is null)
            {
                logger.LogError("Stream entry {EntryId} is malformed; dead-lettering it.", entry.Id);
                await database.StreamAddAsync(DeadLetterKey, entry.Values);
                await database.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, entry.Id);
                return false;
            }

            try
            {
                await dispatcher.Dispatch(envelope, cancellationToken);
                return true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Message {MessageId} ({Name} v{Version}) failed; it stays pending for retry.", envelope.MessageId, envelope.Name, envelope.Version);
                return false;
            }
        }

        private async Task DeadLetter(IDatabase database, StreamPendingMessageInfo info)
        {
            var entries = await database.StreamRangeAsync(_options.StreamKey, info.MessageId, info.MessageId);

            if (entries.Length > 0)
                await database.StreamAddAsync(DeadLetterKey, entries[0].Values);

            await database.StreamAcknowledgeAsync(_options.StreamKey, _options.ConsumerGroup, info.MessageId);

            logger.LogError("Stream entry {EntryId} was dead-lettered after {Attempts} deliveries.", info.MessageId, info.DeliveryCount);
        }
    }
}
