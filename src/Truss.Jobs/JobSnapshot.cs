namespace Truss.Jobs
{
    /// <summary>
    /// The lifecycle status of a job.
    /// </summary>
    public enum JobStatus
    {
        /// <summary>The job waits for its scheduled moment.</summary>
        Scheduled = 0,

        /// <summary>The job waits for a worker.</summary>
        Queued = 1,

        /// <summary>The job is executing.</summary>
        Running = 2,

        /// <summary>The job completed successfully.</summary>
        Succeeded = 3,

        /// <summary>The job exhausted its attempts and failed.</summary>
        Failed = 4,

        /// <summary>The job was cancelled before or during execution.</summary>
        Cancelled = 5
    }

    /// <summary>
    /// A read-only view of a job's state, progress and outcome.
    /// </summary>
    /// <param name="Id">The job identifier.</param>
    /// <param name="Name">The stable name of the job type.</param>
    /// <param name="Status">The lifecycle status.</param>
    /// <param name="Attempts">The number of executions started so far.</param>
    /// <param name="ProgressPercent">The last reported progress percentage.</param>
    /// <param name="ProgressMessage">The last reported progress message.</param>
    /// <param name="Error">The error of the last failed attempt, when any.</param>
    /// <param name="CreatedOn">When the job was created.</param>
    /// <param name="ScheduledFor">When the job is scheduled to run, for scheduled jobs.</param>
    /// <param name="StartedOn">When the last attempt started.</param>
    /// <param name="CompletedOn">When the job reached a terminal status.</param>
    public sealed record JobSnapshot(
        Guid Id,
        string Name,
        JobStatus Status,
        int Attempts,
        int ProgressPercent,
        string? ProgressMessage,
        string? Error,
        DateTimeOffset CreatedOn,
        DateTimeOffset? ScheduledFor,
        DateTimeOffset? StartedOn,
        DateTimeOffset? CompletedOn);

    /// <summary>
    /// Read access to job state, used by progress endpoints and dashboards.
    /// </summary>
    public interface IJobMonitor
    {
        /// <summary>
        /// Returns the current snapshot of a job, or null when it does not exist.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<JobSnapshot?> Get(Guid jobId, CancellationToken cancellationToken = default);
    }
}
