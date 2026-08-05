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
    }
}
