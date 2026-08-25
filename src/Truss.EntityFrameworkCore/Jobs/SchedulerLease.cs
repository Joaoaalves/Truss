using Truss.Jobs;
namespace Truss.EntityFrameworkCore.Jobs
{
    /// <summary>
    /// The persisted lease behind the scheduler lock. One row per lock name;
    /// the expiry acts as the concurrency token, so two instances cannot both
    /// take over an expired lease.
    /// </summary>
    public class SchedulerLease
    {
        private SchedulerLease()
        {
            Name = string.Empty;
            Owner = string.Empty;
        }

        /// <summary>
        /// Creates a lease held by the given owner.
        /// </summary>
        public SchedulerLease(string name, string owner, DateTimeOffset expiresOn)
        {
            Name = name;
            Owner = owner;
            ExpiresOn = expiresOn;
        }

        /// <summary>Gets the lock name.</summary>
        public string Name { get; private set; }

        /// <summary>Gets the identity of the holding instance.</summary>
        public string Owner { get; private set; }

        /// <summary>Gets when the lease expires without renewal.</summary>
        public DateTimeOffset ExpiresOn { get; private set; }

        /// <summary>
        /// Hands the lease to an owner until the new expiry.
        /// </summary>
        public void Renew(string owner, DateTimeOffset expiresOn)
        {
            Owner = owner;
            ExpiresOn = expiresOn;
        }
    }
}
