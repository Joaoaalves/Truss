namespace Truss.EntityFrameworkCore
{
    /// <summary>
    /// The stored response of an idempotent command, keyed by request type plus
    /// the client-supplied idempotency key.
    /// </summary>
    public class IdempotencyRecord
    {
        private IdempotencyRecord()
        {
            Key = string.Empty;
            ResponsePayload = string.Empty;
        }

        /// <summary>
        /// Creates a record for a processed command.
        /// </summary>
        public IdempotencyRecord(string key, string responsePayload, DateTimeOffset processedOn)
        {
            Key = key;
            ResponsePayload = responsePayload;
            ProcessedOn = processedOn;
        }

        /// <summary>Gets the storage key.</summary>
        public string Key { get; private set; }

        /// <summary>Gets the serialized response.</summary>
        public string ResponsePayload { get; private set; }

        /// <summary>Gets when the command was processed.</summary>
        public DateTimeOffset ProcessedOn { get; private set; }
    }
}
