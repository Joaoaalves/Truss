using System.Text.Json;
using Truss.Messaging;

namespace Truss.Jobs
{
    internal sealed class JobScheduler(
        IJobStore store,
        IIntegrationEventPublisher publisher,
        JobTypeRegistry registry,
        TimeProvider timeProvider) : IJobScheduler
    {
        public async Task<Guid> Enqueue<TJob, TArgs>(TArgs args, CancellationToken cancellationToken = default)
            where TJob : IJob<TArgs>
        {
            var descriptor = registry.DescriptorFor(typeof(TJob));
            var record = JobRecord.CreateQueued(
                Guid.NewGuid(), descriptor.Name, JsonSerializer.Serialize(args), timeProvider.GetUtcNow());

            await store.Add(record, cancellationToken);
            await publisher.Publish(new JobEnqueued(record.Id), cancellationToken);

            return record.Id;
        }

        public async Task<Guid> Schedule<TJob, TArgs>(TArgs args, DateTimeOffset runAt, CancellationToken cancellationToken = default)
            where TJob : IJob<TArgs>
        {
            var descriptor = registry.DescriptorFor(typeof(TJob));
            var record = JobRecord.CreateScheduled(
                Guid.NewGuid(), descriptor.Name, JsonSerializer.Serialize(args), timeProvider.GetUtcNow(), runAt);

            await store.Add(record, cancellationToken);

            return record.Id;
        }
    }
}
