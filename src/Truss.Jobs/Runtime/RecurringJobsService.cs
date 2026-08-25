using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Truss.Messaging;
using Truss.Jobs.Storage;

namespace Truss.Jobs.Runtime
{
    internal sealed class RecurringJobsService(
        IServiceScopeFactory scopeFactory,
        JobTypeRegistry registry,
        IOptions<TrussJobsOptions> options,
        ILogger<RecurringJobsService> logger,
        TimeProvider timeProvider) : BackgroundService
    {
        private sealed record RecurringState(RecurringJobDefinition Definition, CronExpression Expression)
        {
            public DateTimeOffset? NextOccurrence { get; set; }
        }

        private readonly TrussJobsOptions _options = options.Value;
        private readonly string _owner = $"{Environment.MachineName}-{Guid.NewGuid():N}";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.EnableSchedulers || _options.Recurring.Count == 0)
                return;

            var states = _options.Recurring
                .Select(definition => new RecurringState(definition, Parse(definition.Cron)))
                .ToList();

            foreach (var state in states)
                state.NextOccurrence = state.Expression.GetNextOccurrence(timeProvider.GetUtcNow(), TimeZoneInfo.Utc);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = timeProvider.GetUtcNow();
                    var due = states.Where(state => state.NextOccurrence is { } next && next <= now).ToList();

                    if (due.Count > 0)
                    {
                        var acquired = await TryAcquireLock(stoppingToken);

                        // Every instance advances its own occurrence timeline; only the
                        // lease holder enqueues, so occurrences are not duplicated.
                        foreach (var state in due)
                        {
                            if (acquired)
                                await EnqueueOccurrence(state.Definition, stoppingToken);

                            state.NextOccurrence = state.Expression.GetNextOccurrence(now, TimeZoneInfo.Utc);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Recurring jobs iteration failed.");
                }

                try
                {
                    await Task.Delay(_options.RecurringTickInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            await ReleaseLock();
        }

        /// <summary>
        /// Hands the lease to another instance on shutdown, so failover does
        /// not wait for it to expire. Best effort: a failure only means the
        /// old behavior, and the lease lapses on its own.
        /// </summary>
        private async Task ReleaseLock()
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISchedulerLock>()
                    .Release(SchedulerLockNames.Recurring, _owner, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Releasing the recurring scheduler lease failed; it will expire on its own.");
            }
        }

        private static CronExpression Parse(string cron)
        {
            var format = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6
                ? CronFormat.IncludeSeconds
                : CronFormat.Standard;

            return CronExpression.Parse(cron, format);
        }

        private async Task<bool> TryAcquireLock(CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var schedulerLock = scope.ServiceProvider.GetRequiredService<ISchedulerLock>();

            return await schedulerLock.TryAcquire(
                SchedulerLockNames.Recurring, _owner, _options.SchedulerLockLeaseDuration, cancellationToken);
        }

        private async Task EnqueueOccurrence(RecurringJobDefinition definition, CancellationToken cancellationToken)
        {
            var descriptor = registry.DescriptorFor(definition.JobType);

            await using var scope = scopeFactory.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IJobStore>();
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();

            var record = JobRecord.CreateQueued(
                Guid.NewGuid(), descriptor.Name, definition.ArgsPayload, timeProvider.GetUtcNow());

            await store.Add(record, cancellationToken);
            await publisher.Publish(new JobEnqueued(record.Id), cancellationToken);
            await JobScopeCommitter.Commit(scope.ServiceProvider, cancellationToken);
        }
    }
}
