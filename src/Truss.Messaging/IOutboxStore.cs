namespace Truss.Messaging
{
    /// <summary>
    /// Persistence contract for the outbox.
    /// Implementations must make <see cref="Add"/> participate in the ambient unit of work,
    /// so the message is stored atomically with the command that published it.
    /// </summary>
    public interface IOutboxStore
    {
        /// <summary>
        /// Stages a message for storage. The message is persisted when the current
        /// unit of work commits, not immediately.
        /// </summary>
        /// <param name="message">The message to store.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Add(OutboxMessage message, CancellationToken cancellationToken = default);

        /// <summary>
        /// Fetches pending messages that are due for a publish attempt, oldest first.
        /// </summary>
        /// <param name="batchSize">The maximum number of messages to fetch.</param>
        /// <param name="now">The current time.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<IReadOnlyList<OutboxMessage>> FetchDue(int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default);

        /// <summary>
        /// Persists status changes made to previously fetched messages.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Save(CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes processed messages older than the given threshold.
        /// Pending and failed messages are never touched.
        /// </summary>
        /// <param name="threshold">The processing time before which messages are deleted.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of deleted messages.</returns>
        Task<int> DeleteProcessedBefore(DateTimeOffset threshold, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns every dead-lettered message to the pending queue with a
        /// clean attempt counter, so the processor delivers it again. Meant for
        /// the moment after the underlying failure was fixed.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of messages returned to the queue.</returns>
        Task<int> RetryDeadLettered(CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the outbox counters used by health checks and diagnostics.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<OutboxStatistics> GetStatistics(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The outbox counters: how many messages wait, how many were dead-lettered
    /// and when the oldest waiting message was produced.
    /// </summary>
    /// <param name="PendingCount">The number of messages waiting for publication.</param>
    /// <param name="FailedCount">The number of dead-lettered messages.</param>
    /// <param name="OldestPendingOccurredOn">When the oldest waiting message was produced, when any.</param>
    public sealed record OutboxStatistics(int PendingCount, int FailedCount, DateTimeOffset? OldestPendingOccurredOn);
}
