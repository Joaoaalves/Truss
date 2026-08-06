namespace Truss.Application
{
    /// <summary>
    /// Persistence contract for idempotent command responses.
    /// Implementations must make <see cref="Add"/> participate in the ambient unit
    /// of work, so the record commits atomically with the command it protects.
    /// </summary>
    public interface IIdempotencyStore
    {
        /// <summary>
        /// Returns the stored response payload for a key, or null when the key is new.
        /// </summary>
        /// <param name="key">The storage key.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<string?> FindResponse(string key, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stages a response for storage. Persisted when the current unit of work
        /// commits, never before.
        /// </summary>
        /// <param name="key">The storage key.</param>
        /// <param name="responsePayload">The serialized response.</param>
        /// <param name="processedOn">When the command was processed.</param>
        void Add(string key, string responsePayload, DateTimeOffset processedOn);

        /// <summary>
        /// Deletes records processed before the given threshold.
        /// </summary>
        /// <param name="threshold">The processing time before which records are deleted.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The number of deleted records.</returns>
        Task<int> DeleteBefore(DateTimeOffset threshold, CancellationToken cancellationToken = default);
    }
}
