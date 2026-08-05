namespace Truss.Jobs
{
    /// <summary>
    /// A background job. Implementations are resolved from the dependency injection scope
    /// of each execution, receive their deserialized arguments and report progress through the context.
    /// Delivery is at-least-once: a job may run more than once for the same arguments.
    /// </summary>
    /// <typeparam name="TArgs">The type of the job arguments. Must be JSON-serializable.</typeparam>
    public interface IJob<TArgs>
    {
        /// <summary>
        /// Executes the job.
        /// </summary>
        /// <param name="args">The job arguments.</param>
        /// <param name="context">The execution context, used to report progress.</param>
        /// <param name="cancellationToken">Canceled on shutdown or when the job times out.</param>
        Task Execute(TArgs args, JobContext context, CancellationToken cancellationToken);
    }
}
