namespace Truss.Jobs
{
    /// <summary>
    /// Elects the instance that runs a scheduler sweep.
    /// Acquiring grants a lease; the holder renews it by acquiring again, and when it
    /// stops renewing, another instance takes over once the lease expires.
    /// </summary>
    public interface ISchedulerLock
    {
        /// <summary>
        /// Tries to acquire or renew the named lease for the given owner.
        /// </summary>
        /// <param name="name">The lock name.</param>
        /// <param name="owner">The identity of the acquiring instance.</param>
        /// <param name="leaseDuration">How long the lease lasts without renewal.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>True when this owner holds the lease.</returns>
        Task<bool> TryAcquire(string name, string owner, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

        /// <summary>
        /// Releases the named lease when this owner holds it, so a graceful
        /// shutdown hands the schedulers to another instance immediately
        /// instead of making it wait for the lease to expire.
        /// </summary>
        /// <param name="name">The lock name.</param>
        /// <param name="owner">The identity of the releasing instance.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Release(string name, string owner, CancellationToken cancellationToken = default);
    }

    internal static class SchedulerLockNames
    {
        public const string Scheduled = "truss.jobs.scheduled";
        public const string Recurring = "truss.jobs.recurring";
    }

    /// <summary>
    /// Single-instance lock: always grants. Used with the in-memory store,
    /// where only one process exists by definition.
    /// </summary>
    public sealed class LocalSchedulerLock : ISchedulerLock
    {
        /// <inheritdoc />
        public Task<bool> TryAcquire(string name, string owner, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public Task Release(string name, string owner, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
