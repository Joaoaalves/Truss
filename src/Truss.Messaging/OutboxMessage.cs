namespace Truss.Messaging
{
    /// <summary>
    /// The delivery status of an outbox message.
    /// </summary>
    public enum OutboxMessageStatus
    {
        /// <summary>The message is waiting to be published.</summary>
        Pending = 0,

        /// <summary>The message was published successfully.</summary>
        Processed = 1,

        /// <summary>The message exhausted its attempts and was dead-lettered.</summary>
        Failed = 2
    }

    /// <summary>
    /// An integration event stored transactionally, waiting to be published to the transport.
    /// </summary>
    public class OutboxMessage
    {
        private OutboxMessage()
        {
            Name = string.Empty;
            Payload = string.Empty;
        }

        /// <summary>
        /// Initializes a new pending outbox message.
        /// </summary>
        /// <param name="id">The identifier of the event instance.</param>
        /// <param name="name">The stable wire name of the event.</param>
        /// <param name="version">The version of the event contract.</param>
        /// <param name="payload">The JSON payload of the event.</param>
        /// <param name="occurredOn">The timestamp of the event.</param>
        /// <param name="traceParent">The W3C traceparent of the publishing operation, when tracing was active.</param>
        public OutboxMessage(Guid id, string name, int version, string payload, DateTimeOffset occurredOn, string? traceParent = null)
        {
            Id = id;
            Name = name;
            Version = version;
            Payload = payload;
            OccurredOn = occurredOn;
            TraceParent = traceParent;
        }

        /// <summary>Gets the identifier of the event instance.</summary>
        public Guid Id { get; private set; }

        /// <summary>Gets the stable wire name of the event.</summary>
        public string Name { get; private set; }

        /// <summary>Gets the version of the event contract.</summary>
        public int Version { get; private set; }

        /// <summary>Gets the JSON payload of the event.</summary>
        public string Payload { get; private set; }

        /// <summary>Gets the timestamp of the event.</summary>
        public DateTimeOffset OccurredOn { get; private set; }

        /// <summary>Gets the W3C traceparent of the publishing operation, when tracing was active.</summary>
        public string? TraceParent { get; private set; }

        /// <summary>Gets the delivery status of the message.</summary>
        public OutboxMessageStatus Status { get; private set; } = OutboxMessageStatus.Pending;

        /// <summary>Gets the number of failed publish attempts.</summary>
        public int Attempts { get; private set; }

        /// <summary>Gets the earliest moment the next attempt may run, when a retry is scheduled.</summary>
        public DateTimeOffset? NextAttemptOn { get; private set; }

        /// <summary>Gets the moment the message was published, when processed.</summary>
        public DateTimeOffset? ProcessedOn { get; private set; }

        /// <summary>Gets the error message of the last failed attempt.</summary>
        public string? Error { get; private set; }

        /// <summary>
        /// Converts the stored message back into its wire envelope.
        /// </summary>
        public IntegrationEventEnvelope ToEnvelope()
        {
            return new IntegrationEventEnvelope(Id, Name, Version, OccurredOn, Payload, TraceParent);
        }

        /// <summary>
        /// Marks the message as successfully published.
        /// </summary>
        /// <param name="now">The current time.</param>
        public void MarkProcessed(DateTimeOffset now)
        {
            Status = OutboxMessageStatus.Processed;
            ProcessedOn = now;
            Error = null;
            NextAttemptOn = null;
        }

        /// <summary>
        /// Records a failed publish attempt.
        /// Schedules a retry, or dead-letters the message once the attempt limit is reached.
        /// </summary>
        /// <param name="error">The error of the failed attempt.</param>
        /// <param name="now">The current time.</param>
        /// <param name="retryDelay">The delay before the next attempt.</param>
        /// <param name="maxAttempts">The attempt limit before dead-lettering.</param>
        public void RegisterFailure(string error, DateTimeOffset now, TimeSpan retryDelay, int maxAttempts)
        {
            Attempts++;
            Error = error;

            if (Attempts >= maxAttempts)
            {
                Status = OutboxMessageStatus.Failed;
                NextAttemptOn = null;
            }
            else
            {
                NextAttemptOn = now + retryDelay;
            }
        }
    }
}
