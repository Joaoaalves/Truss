using Microsoft.EntityFrameworkCore;

namespace Truss.Messaging.EntityFrameworkCore
{
    /// <summary>
    /// EF Core inbox store.
    /// Records are added to the context without saving, so they are persisted by
    /// the unit of work of the message being handled. A duplicate delivery
    /// committing concurrently violates the primary key and rolls back with all
    /// the handler's changes, which is what makes the side effects exactly-once.
    /// </summary>
    /// <typeparam name="TDbContext">The context that owns the inbox table.</typeparam>
    public class EfInboxStore<TDbContext>(TDbContext context) : IInboxStore
        where TDbContext : DbContext
    {
        private readonly TDbContext _context = context;

        /// <inheritdoc />
        public Task<bool> AlreadyProcessed(Guid messageId, CancellationToken cancellationToken = default)
        {
            return _context.Set<InboxRecord>().AnyAsync(record => record.MessageId == messageId, cancellationToken);
        }

        /// <inheritdoc />
        public Task MarkProcessed(Guid messageId, string name, DateTimeOffset processedOn, CancellationToken cancellationToken = default)
        {
            _context.Set<InboxRecord>().Add(new InboxRecord(messageId, name, processedOn));
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<int> DeleteProcessedBefore(DateTimeOffset threshold, CancellationToken cancellationToken = default)
        {
            return _context.Set<InboxRecord>()
                .Where(record => record.ProcessedOn < threshold)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
