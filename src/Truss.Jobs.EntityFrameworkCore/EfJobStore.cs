using Microsoft.EntityFrameworkCore;

namespace Truss.Jobs.EntityFrameworkCore
{
    /// <summary>
    /// EF Core job store.
    /// Records are added to the context without saving, so scheduling inside a command
    /// is persisted atomically by the command's unit of work.
    /// </summary>
    /// <typeparam name="TDbContext">The context that owns the job table.</typeparam>
    public class EfJobStore<TDbContext>(TDbContext context) : IJobStore
        where TDbContext : DbContext
    {
        private readonly TDbContext _context = context;

        /// <inheritdoc />
        public Task Add(JobRecord record, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(record);

            _context.Set<JobRecord>().Add(record);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<JobRecord?> Get(Guid jobId, CancellationToken cancellationToken = default)
        {
            return await _context.Set<JobRecord>()
                .FirstOrDefaultAsync(record => record.Id == jobId, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<JobRecord>> FetchDueScheduled(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            return await _context.Set<JobRecord>()
                .Where(record => record.Status == JobStatus.Scheduled && record.ScheduledFor <= now)
                .OrderBy(record => record.ScheduledFor)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task Save(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
