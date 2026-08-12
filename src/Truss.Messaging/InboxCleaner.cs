using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Truss.Messaging
{
    /// <summary>
    /// Background service that sweeps processed inbox records past their
    /// retention, so the table stays proportional to recent traffic.
    /// </summary>
    public sealed class InboxCleaner(
        IServiceScopeFactory scopeFactory,
        IOptions<TrussInboxOptions> options,
        ILogger<InboxCleaner> logger,
        TimeProvider timeProvider) : BackgroundService
    {
        private readonly TrussInboxOptions _options = options.Value;

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (_options.RetentionPeriod is not { } retention)
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var store = scope.ServiceProvider.GetRequiredService<IInboxStore>();

                    var deleted = await store.DeleteProcessedBefore(timeProvider.GetUtcNow() - retention, stoppingToken);

                    if (deleted > 0)
                        logger.LogInformation("Inbox cleanup removed {Count} records older than {Retention}.", deleted, retention);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Inbox cleanup iteration failed.");
                }

                try
                {
                    await Task.Delay(_options.CleanupInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
