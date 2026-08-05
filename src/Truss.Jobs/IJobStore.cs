namespace Truss.Jobs
{
    /// <summary>
    /// Persistence contract for job records.
    /// Implementations backed by the application database make <see cref="Add"/> participate
    /// in the ambient unit of work, so scheduling is atomic with the command.
    /// </summary>
    public interface IJobStore
    {
        /// <summary>
        /// Stages a job record for storage. Persisted when the current unit of work commits,
        /// or on the next <see cref="Save"/> call outside a command.
        /// </summary>
        /// <param name="record">The record to store.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Add(JobRecord record, CancellationToken cancellationToken = default);

        /// <summary>
        /// Loads a job record for update.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<JobRecord?> Get(Guid jobId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches scheduled jobs whose moment has arrived, oldest first.
        /// </summary>
        /// <param name="batchSize">The maximum number of jobs to fetch.</param>
        /// <param name="now">The current time.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<IReadOnlyList<JobRecord>> FetchDueScheduled(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists changes made to staged or fetched records.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Save(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes succeeded and cancelled jobs that finished before the given threshold.
        /// Failed jobs are never touched.
        /// </summary>
        /// <param name="threshold">The completion time before which jobs are deleted.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of deleted jobs.</returns>
        Task<int> DeleteFinishedBefore(DateTimeOffset threshold, CancellationToken cancellationToken = default);
    }
}
