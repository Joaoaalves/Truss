using Microsoft.EntityFrameworkCore.Diagnostics;
using Truss.Messaging;
using Truss.Messaging.Outbox;

namespace Truss.EntityFrameworkCore.Messaging
{
    internal sealed class OutboxCommitInterceptor(OutboxSignal signal) : SaveChangesInterceptor
    {
        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            NotifyIfOutboxTracked(eventData);
            return base.SavedChanges(eventData, result);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            NotifyIfOutboxTracked(eventData);
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }

        private void NotifyIfOutboxTracked(SaveChangesCompletedEventData eventData)
        {
            if (eventData.Context is { } context
                && context.ChangeTracker.Entries<OutboxMessage>().Any())
            {
                signal.Notify();
            }
        }
    }
}
