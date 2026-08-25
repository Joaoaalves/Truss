namespace Truss.Messaging.Inbox
{
    /// <summary>
    /// Persistence contract for the inbox, the consumer-side half of exactly-once.
    /// Implementations must make <see cref="MarkProcessed"/> participate in the
    /// ambient unit of work, so the record is stored atomically with the
    /// handler's changes: a duplicate delivery arriving concurrently collides on
    /// the message id, its transaction rolls back, and the side effects of the
    /// second handling never commit.
    /// </summary>
    public interface IInboxStore
    {
        /// <summary>
        /// Determines whether a message was already processed by this consumer.
        /// </summary>
        /// <param name="messageId">The identifier of the message.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<bool> AlreadyProcessed(Guid messageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages the record of a processed message. The record is persisted when
        /// the message's unit of work commits, not immediately.
        /// </summary>
        /// <param name="messageId">The identifier of the message.</param>
        /// <param name="name">The wire name of the event, kept for diagnostics.</param>
        /// <param name="processedOn">When the message was processed.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task MarkProcessed(Guid messageId, string name, DateTimeOffset processedOn, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes inbox records older than the given threshold. A record only
        /// needs to outlive the window in which its message could be redelivered.
        /// </summary>
        /// <param name="threshold">The processing time before which records are deleted.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of deleted records.</returns>
        Task<int> DeleteProcessedBefore(DateTimeOffset threshold, CancellationToken cancellationToken = default);
    }
}
