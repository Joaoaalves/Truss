namespace Truss.Jobs
{
    /// <summary>
    /// Schedules background jobs.
    /// Called inside a command handler, scheduling participates in the command's transaction:
    /// the job exists only if the command commits.
    /// </summary>
    public interface IJobScheduler
    {
        /// <summary>
        /// Enqueues a job for execution as soon as a worker picks it up.
        /// </summary>
        /// <typeparam name="TJob">The job type.</typeparam>
        /// <typeparam name="TArgs">The type of the job arguments.</typeparam>
        /// <param name="args">The job arguments.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The identifier of the job, usable to track progress.</returns>
        Task<Guid> Enqueue<TJob, TArgs>(TArgs args, CancellationToken cancellationToken = default)
            where TJob : IJob<TArgs>;

        /// <summary>
        /// Schedules a job to run at a specific moment.
        /// </summary>
        /// <typeparam name="TJob">The job type.</typeparam>
        /// <typeparam name="TArgs">The type of the job arguments.</typeparam>
        /// <param name="args">The job arguments.</param>
        /// <param name="runAt">The earliest moment the job may run.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The identifier of the job, usable to track progress.</returns>
        Task<Guid> Schedule<TJob, TArgs>(TArgs args, DateTimeOffset runAt, CancellationToken cancellationToken = default)
            where TJob : IJob<TArgs>;

        /// <summary>
        /// Requests the cancellation of a job. A queued or scheduled job is cancelled
        /// immediately; a running job observes the request through its cancellation token
        /// and stops at the next cancellation point. Terminal jobs are left untouched.
        /// </summary>
        /// <param name="jobId">The job identifier.</param>
        /// <param name="cancellationToken">The cancellation token of this call.</param>
        /// <returns>True when the job exists; false when it is unknown.</returns>
        Task<bool> Cancel(Guid jobId, CancellationToken cancellationToken = default);
    }
}
