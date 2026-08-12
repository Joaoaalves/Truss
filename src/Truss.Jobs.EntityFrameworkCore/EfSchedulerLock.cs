using Microsoft.EntityFrameworkCore;

namespace Truss.Jobs.EntityFrameworkCore
{
    /// <summary>
    /// EF Core scheduler lock backed by a lease row.
    /// A missing lease is inserted, an expired or owned one is renewed; the primary key
    /// and the expiry concurrency token turn every race into a clean acquisition failure.
    /// </summary>
    /// <typeparam name="TDbContext">The context that owns the lease table.</typeparam>
    public class EfSchedulerLock<TDbContext>(TDbContext context, TimeProvider timeProvider) : ISchedulerLock
        where TDbContext : DbContext
    {
        private readonly TDbContext _context = context;

        /// <inheritdoc />
        public async Task<bool> TryAcquire(string name, string owner, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
        {
            var now = timeProvider.GetUtcNow();
            var lease = await _context.Set<SchedulerLease>()
                .FirstOrDefaultAsync(l => l.Name == name, cancellationToken);

            if (lease is null)
            {
                _context.Set<SchedulerLease>().Add(new SchedulerLease(name, owner, now + leaseDuration));
                return await TrySave(cancellationToken);
            }

            if (lease.Owner != owner && lease.ExpiresOn > now)
                return false;

            lease.Renew(owner, now + leaseDuration);
            return await TrySave(cancellationToken);
        }

        /// <inheritdoc />
        public async Task Release(string name, string owner, CancellationToken cancellationToken = default)
        {
            var lease = await _context.Set<SchedulerLease>()
                .FirstOrDefaultAsync(l => l.Name == name && l.Owner == owner, cancellationToken);

            if (lease is null)
                return;

            // Expiring the lease instead of deleting it keeps the row and its
            // concurrency token; a racing renewal simply fails to save.
            lease.Renew(owner, timeProvider.GetUtcNow());
            await TrySave(cancellationToken);
        }

        private async Task<bool> TrySave(CancellationToken cancellationToken)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                return false;
            }
        }
    }
}
