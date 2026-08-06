using Microsoft.EntityFrameworkCore;

namespace Truss.Messaging.EntityFrameworkCore
{
    /// <summary>
    /// EF Core outbox store.
    /// Messages are added to the context without saving, so they are persisted atomically
    /// by the unit of work of the command that published them.
    /// </summary>
    /// <typeparam name="TDbContext">The context that owns the outbox table.</typeparam>
    public class EfOutboxStore<TDbContext>(TDbContext context) : IOutboxStore
        where TDbContext : DbContext
    {
        private readonly TDbContext _context = context;

        /// <inheritdoc />
        public Task Add(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);

            _context.Set<OutboxMessage>().Add(message);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<OutboxMessage>> FetchDue(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            return await _context.Set<OutboxMessage>()
                .Where(message => message.Status == OutboxMessageStatus.Pending
                    && (message.NextAttemptOn == null || message.NextAttemptOn <= now))
                .OrderBy(message => message.OccurredOn)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task Save(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<int> DeleteProcessedBefore(DateTimeOffset threshold, CancellationToken cancellationToken = default)
        {
            return _context.Set<OutboxMessage>()
                .Where(message => message.Status == OutboxMessageStatus.Processed && message.ProcessedOn < threshold)
                .ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<OutboxStatistics> GetStatistics(CancellationToken cancellationToken = default)
        {
            var pending = _context.Set<OutboxMessage>().Where(message => message.Status == OutboxMessageStatus.Pending);

            return new OutboxStatistics(
                await pending.CountAsync(cancellationToken),
                await _context.Set<OutboxMessage>().CountAsync(message => message.Status == OutboxMessageStatus.Failed, cancellationToken),
                await pending.MinAsync(message => (DateTimeOffset?)message.OccurredOn, cancellationToken));
        }
    }
}
