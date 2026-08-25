using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Truss.Messaging;
using Truss.Messaging.Outbox;

namespace Truss.EntityFrameworkCore.Messaging
{
    /// <summary>
    /// EF Core outbox store.
    /// Messages are added to the context without saving, so they are persisted atomically
    /// by the unit of work of the command that published them.
    /// On PostgreSQL and SQL Server a fetch claims its rows with SKIP LOCKED semantics
    /// until <see cref="Save"/> commits, so instances publishing concurrently pick
    /// disjoint batches instead of duplicating each other's messages.
    /// </summary>
    /// <typeparam name="TDbContext">The context that owns the outbox table.</typeparam>
    public class EfOutboxStore<TDbContext>(TDbContext context) : IOutboxStore
        where TDbContext : DbContext
    {
        private readonly TDbContext _context = context;
        private IDbContextTransaction? _claim;

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
            if (ClaimSql() is not { } sql)
            {
                return await _context.Set<OutboxMessage>()
                    .Where(message => message.Status == OutboxMessageStatus.Pending
                        && (message.NextAttemptOn == null || message.NextAttemptOn <= now))
                    .OrderBy(message => message.OccurredOn)
                    .Take(batchSize)
                    .ToListAsync(cancellationToken);
            }

            // The row locks live as long as this transaction, so the claim spans
            // the publish loop and is released by Save, or rolled back with the
            // scope when the processor dies mid-batch.
            _claim = await _context.Database.BeginTransactionAsync(cancellationToken);

            var messages = await _context.Set<OutboxMessage>()
                .FromSqlRaw(sql, batchSize, now.UtcTicks)
                .ToListAsync(cancellationToken);

            if (messages.Count == 0)
                await ReleaseClaim(cancellationToken);

            return messages;
        }

        /// <inheritdoc />
        public async Task Save(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
            await ReleaseClaim(cancellationToken);
        }

        /// <inheritdoc />
        public Task<int> DeleteProcessedBefore(DateTimeOffset threshold, CancellationToken cancellationToken = default)
        {
            return _context.Set<OutboxMessage>()
                .Where(message => message.Status == OutboxMessageStatus.Processed && message.ProcessedOn < threshold)
                .ExecuteDeleteAsync(cancellationToken);
        }

        /// <inheritdoc />
        public Task<int> RetryDeadLettered(CancellationToken cancellationToken = default)
        {
            return _context.Set<OutboxMessage>()
                .Where(message => message.Status == OutboxMessageStatus.Failed)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(message => message.Status, OutboxMessageStatus.Pending)
                    .SetProperty(message => message.Attempts, 0)
                    .SetProperty(message => message.NextAttemptOn, (DateTimeOffset?)null)
                    .SetProperty(message => message.Error, (string?)null),
                    cancellationToken);
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

        private async Task ReleaseClaim(CancellationToken cancellationToken)
        {
            if (_claim is null)
                return;

            await _claim.CommitAsync(cancellationToken);
            await _claim.DisposeAsync();
            _claim = null;
        }

        /// <summary>
        /// Builds the locking fetch for providers that can skip locked rows, from
        /// the mapped table and column names. Providers without the feature, and
        /// contexts already inside a transaction, fall back to the plain query.
        /// </summary>
        private string? ClaimSql()
        {
            if (_context.Database.CurrentTransaction is not null)
                return null;

            var provider = _context.Database.ProviderName;

            if (provider is null)
                return null;

            var postgres = provider.Contains("Npgsql", StringComparison.Ordinal);

            if (!postgres && !provider.Contains("SqlServer", StringComparison.Ordinal))
                return null;

            var entityType = _context.Model.FindEntityType(typeof(OutboxMessage));
            var tableName = entityType?.GetTableName();

            if (entityType is null || tableName is null)
                return null;

            var schema = entityType.GetSchema();
            var table = StoreObjectIdentifier.Table(tableName, schema);

            string Quote(string name) => postgres ? $"\"{name}\"" : $"[{name}]";
            string Column(string property) => Quote(entityType.FindProperty(property)!.GetColumnName(table)!);

            var qualified = schema is null ? Quote(tableName) : $"{Quote(schema)}.{Quote(tableName)}";
            var status = Column(nameof(OutboxMessage.Status));
            var nextAttempt = Column(nameof(OutboxMessage.NextAttemptOn));
            var occurred = Column(nameof(OutboxMessage.OccurredOn));
            var pendingValue = (int)OutboxMessageStatus.Pending;

            return postgres
                ? $"SELECT * FROM {qualified} WHERE {status} = {pendingValue} AND ({nextAttempt} IS NULL OR {nextAttempt} <= {{1}}) ORDER BY {occurred} LIMIT {{0}} FOR UPDATE SKIP LOCKED"
                : $"SELECT TOP ({{0}}) * FROM {qualified} WITH (UPDLOCK, READPAST, ROWLOCK) WHERE {status} = {pendingValue} AND ({nextAttempt} IS NULL OR {nextAttempt} <= {{1}}) ORDER BY {occurred}";
        }
    }
}
