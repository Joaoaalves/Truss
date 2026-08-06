using Microsoft.EntityFrameworkCore;
using Truss.Application;

namespace Truss.Persistence.EntityFrameworkCore
{
    /// <summary>
    /// EF Core idempotency store.
    /// Records are added to the context without saving, so they commit atomically
    /// with the command they protect. When two requests race the same key, the
    /// primary key lets exactly one commit win; the loser rolls back entirely.
    /// </summary>
    /// <typeparam name="TDbContext">The context that owns the idempotency table.</typeparam>
    public class EfIdempotencyStore<TDbContext>(TDbContext context) : IIdempotencyStore
        where TDbContext : DbContext
    {
        private readonly TDbContext _context = context;

        /// <inheritdoc />
        public async Task<string?> FindResponse(string key, CancellationToken cancellationToken = default)
        {
            var record = await _context.Set<IdempotencyRecord>().FindAsync([key], cancellationToken);
            return record?.ResponsePayload;
        }

        /// <inheritdoc />
        public void Add(string key, string responsePayload, DateTimeOffset processedOn)
        {
            _context.Set<IdempotencyRecord>().Add(new IdempotencyRecord(key, responsePayload, processedOn));
        }

        /// <inheritdoc />
        public Task<int> DeleteBefore(DateTimeOffset threshold, CancellationToken cancellationToken = default)
        {
            return _context.Set<IdempotencyRecord>()
                .Where(record => record.ProcessedOn < threshold)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
