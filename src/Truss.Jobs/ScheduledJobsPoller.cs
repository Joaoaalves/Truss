using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Truss.Messaging;

namespace Truss.Jobs
{
    internal sealed class ScheduledJobsPoller(
        IServiceScopeFactory scopeFactory,
        IOptions<TrussJobsOptions> options,
        ILogger<ScheduledJobsPoller> logger,
        TimeProvider timeProvider) : BackgroundService
    {
        private readonly TrussJobsOptions _options = options.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.EnableSchedulers)
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                var moved = 0;

                try
                {
                    moved = await MoveDueJobsToQueue(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Scheduled jobs polling iteration failed.");
                }

                if (moved == 0)
                {
                    try
                    {
                        await Task.Delay(_options.ScheduledPollingInterval, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task<int> MoveDueJobsToQueue(CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

            var due = await store.FetchDueScheduled(50, timeProvider.GetUtcNow(), cancellationToken);

            foreach (var record in due)
            {
                record.MarkQueued();
                await publisher.Publish(new JobEnqueued(record.Id), cancellationToken);
            }

            if (due.Count > 0)
                await JobScopeCommitter.Commit(scope.ServiceProvider, cancellationToken);

            return due.Count;
        }
    }
}
