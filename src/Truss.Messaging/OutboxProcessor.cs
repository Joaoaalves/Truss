using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Truss.Messaging
{
    /// <summary>
    /// Background service that publishes pending outbox messages to the transport.
    /// Failed attempts are retried with exponential backoff; messages that exhaust
    /// their attempts are dead-lettered with the error preserved.
    /// </summary>
    public class OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IMessageTransport transport,
        IOptions<TrussOutboxOptions> options,
        ILogger<OutboxProcessor> logger,
        TimeProvider timeProvider) : BackgroundService
    {
        private static readonly ActivitySource Source = new("Truss.Messaging");

        private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
        private readonly IMessageTransport _transport = transport;
        private readonly TrussOutboxOptions _options = options.Value;
        private readonly ILogger<OutboxProcessor> _logger = logger;
        private readonly TimeProvider _timeProvider = timeProvider;

        /// <summary>
        /// Publishes every due message once and persists the outcome.
        /// Exposed so hosts without a background loop can pump the outbox manually.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of messages that had a publish attempt.</returns>
        public async Task<int> ProcessPendingAsync(CancellationToken cancellationToken = default)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            var now = _timeProvider.GetUtcNow();
            var messages = await store.FetchDue(_options.BatchSize, now, cancellationToken);

            foreach (var message in messages)
            {
                using var activity = Source.StartActivity($"publish {message.Name}");
                activity?.SetTag("truss.event", message.Name);
                activity?.SetTag("truss.message_id", message.Id);

                try
                {
                    await _transport.Publish(message.ToEnvelope(), cancellationToken);
                    message.MarkProcessed(_timeProvider.GetUtcNow());
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

                    message.RegisterFailure(
                        exception.Message,
                        _timeProvider.GetUtcNow(),
                        RetryDelayFor(message.Attempts),
                        _options.MaxAttempts);

                    if (message.Status == OutboxMessageStatus.Failed)
                        _logger.LogError(exception, "Outbox message {MessageId} ({Name} v{Version}) was dead-lettered after {Attempts} attempts.", message.Id, message.Name, message.Version, message.Attempts);
                    else
                        _logger.LogWarning(exception, "Outbox message {MessageId} ({Name} v{Version}) failed attempt {Attempts}; retrying at {NextAttemptOn}.", message.Id, message.Name, message.Version, message.Attempts, message.NextAttemptOn);
                }
            }

            if (messages.Count > 0)
                await store.Save(cancellationToken);

            return messages.Count;
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var processed = 0;

                try
                {
                    processed = await ProcessPendingAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Outbox processing iteration failed.");
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

        /// <summary>
        /// Computes the exponential backoff delay for the next attempt.
        /// </summary>
        private TimeSpan RetryDelayFor(int attemptsSoFar)
        {
            var delay = _options.RetryBaseDelay * Math.Pow(2, attemptsSoFar);
            return delay > _options.RetryMaxDelay ? _options.RetryMaxDelay : delay;
        }
    }
}
