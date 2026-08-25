namespace Truss.Messaging.Inbox
{
    /// <summary>
    /// The record of a message this consumer already processed. Its primary key
    /// is the message id, which is what turns a concurrent duplicate into a
    /// clean constraint violation instead of a double side effect.
    /// </summary>
    public class InboxRecord
    {
        private InboxRecord()
        {
            Name = string.Empty;
        }

        /// <summary>
        /// Initializes the record of a processed message.
        /// </summary>
        /// <param name="messageId">The identifier of the message.</param>
        /// <param name="name">The wire name of the event.</param>
        /// <param name="processedOn">When the message was processed.</param>
        public InboxRecord(Guid messageId, string name, DateTimeOffset processedOn)
        {
            MessageId = messageId;
            Name = name;
            ProcessedOn = processedOn;
        }

        /// <summary>Gets the identifier of the message.</summary>
        public Guid MessageId { get; private set; }

        /// <summary>Gets the wire name of the event.</summary>
        public string Name { get; private set; }

        /// <summary>Gets when the message was processed.</summary>
        public DateTimeOffset ProcessedOn { get; private set; }
    }
}
