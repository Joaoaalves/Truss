using System.Collections.Concurrent;

namespace Truss.Jobs.Storage
{
    /// <summary>
    /// In-process job store. Intended for development and tests;
    /// records are lost when the process stops.
    /// </summary>
    public sealed class InMemoryJobStore : IJobStore
    {
        private readonly ConcurrentDictionary<Guid, JobRecord> _records = new();

        /// <inheritdoc />
        public Task Add(JobRecord record, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            _records[record.Id] = record;
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<JobRecord?> Get(Guid jobId, CancellationToken cancellationToken = default)
        {
            _records.TryGetValue(jobId, out var record);
            return Task.FromResult(record);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<JobRecord>> FetchDueScheduled(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<JobRecord> due = _records.Values
                .Where(record => record.Status == JobStatus.Scheduled && record.ScheduledFor <= now)
                .OrderBy(record => record.ScheduledFor)
                .Take(batchSize)
                .ToList();

            return Task.FromResult(due);
        }

        /// <inheritdoc />
        public Task Save(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<JobStatistics> GetStatistics(CancellationToken cancellationToken = default)
        {
            var records = _records.Values.ToList();

            return Task.FromResult(new JobStatistics(
                records.Count(record => record.Status is JobStatus.Queued or JobStatus.Scheduled),
                records.Count(record => record.Status == JobStatus.Running),
                records.Count(record => record.Status == JobStatus.Failed)));
        }

        /// <inheritdoc />
        public Task<int> DeleteFinishedBefore(DateTimeOffset threshold, CancellationToken cancellationToken = default)
        {
            var finished = _records.Values
                .Where(record => record.Status is JobStatus.Succeeded or JobStatus.Cancelled
                    && record.CompletedOn < threshold)
                .ToList();

            var deleted = 0;

            foreach (var record in finished)
            {
                if (_records.TryRemove(record.Id, out _))
                    deleted++;
            }

            return Task.FromResult(deleted);
        }
    }
}
