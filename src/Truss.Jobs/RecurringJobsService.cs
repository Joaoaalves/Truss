using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Truss.Messaging;

namespace Truss.Jobs
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

                    foreach (var state in states)
                    {
                        if (state.NextOccurrence is not { } next || next > now)
                            continue;

                        await EnqueueOccurrence(state.Definition, stoppingToken);
                        state.NextOccurrence = state.Expression.GetNextOccurrence(now, TimeZoneInfo.Utc);
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
        }

        private static CronExpression Parse(string cron)
        {
            var format = cron.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6
                ? CronFormat.IncludeSeconds
                : CronFormat.Standard;

            return CronExpression.Parse(cron, format);
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
