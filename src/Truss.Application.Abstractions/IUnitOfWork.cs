namespace Truss.Application
{
    /// <summary>
    /// Coordinates persistence and domain event dispatching for a single command execution.
    /// The unit of work is committed automatically by the pipeline and is never called from application code.
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Dispatches pending domain events and persists all changes atomically.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of state entries written to the underlying store.</returns>
        Task<int> CommitAsync(CancellationToken cancellationToken = default);
    }
}
