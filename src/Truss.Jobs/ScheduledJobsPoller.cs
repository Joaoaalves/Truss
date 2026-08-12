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
        TimeProvider timeProvider,
        JobMetrics metrics) : BackgroundService
    {
        private readonly TrussJobsOptions _options = options.Value;
        private readonly string _owner = $"{Environment.MachineName}-{Guid.NewGuid():N}";
        private DateTimeOffset _nextCleanup = DateTimeOffset.MinValue;
        private DateTimeOffset _nextStatisticsSample = DateTimeOffset.MinValue;

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
            var schedulerLock = scope.ServiceProvider.GetRequiredService<ISchedulerLock>();

            var acquired = await schedulerLock.TryAcquire(
                SchedulerLockNames.Scheduled, _owner, _options.SchedulerLockLeaseDuration, cancellationToken);

            if (!acquired)
                return 0;

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

            await CleanupIfDue(store, cancellationToken);
            await SampleStatisticsIfDue(store, cancellationToken);

            return due.Count;
        }

        /// <summary>
        /// Refreshes the queue gauges from the store, at most every 30 seconds,
        /// so the gauges cost one count query and not one per poll.
        /// </summary>
        private async Task SampleStatisticsIfDue(IJobStore store, CancellationToken cancellationToken)
        {
            var now = timeProvider.GetUtcNow();

            if (now < _nextStatisticsSample)
                return;

            _nextStatisticsSample = now + TimeSpan.FromSeconds(30);
            metrics.DepthSampled(await store.GetStatistics(cancellationToken));
        }

        /// <summary>
        /// Sweeps finished jobs past their retention, at most once per cleanup interval.
        /// Runs behind the scheduler lock, so a single instance cleans.
        /// </summary>
        private async Task CleanupIfDue(IJobStore store, CancellationToken cancellationToken)
        {
            if (_options.RetentionPeriod is not { } retention)
                return;

            var now = timeProvider.GetUtcNow();

            if (now < _nextCleanup)
                return;

            _nextCleanup = now + _options.CleanupInterval;

            var deleted = await store.DeleteFinishedBefore(now - retention, cancellationToken);

            if (deleted > 0)
                logger.LogInformation("Job cleanup removed {Count} finished jobs older than {Retention}.", deleted, retention);
        }
    }
}
