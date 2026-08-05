namespace Truss.Messaging.RabbitMq
{
    /// <summary>
    /// Options for the RabbitMQ transport.
    /// Bindable from configuration, for example the "Truss:Messaging:RabbitMq" section or
    /// environment variables such as Truss__Messaging__RabbitMq__ConnectionString.
    /// </summary>
    public sealed class TrussRabbitMqTransportOptions
    {
        /// <summary>
        /// Gets or sets the AMQP connection string, for example
        /// "amqp://guest:guest@localhost:5672". Required.
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the queue that carries the messages. Defaults to "truss.messages".
        /// Dead-lettered messages go to the same name with a ".dead" suffix.
        /// </summary>
        public string QueueName { get; set; } = "truss.messages";

        /// <summary>
        /// Gets or sets whether this application consumes messages. Defaults to true.
        /// Disable on publisher-only applications.
        /// </summary>
        public bool EnableConsumer { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of unacknowledged messages the broker delivers ahead.
        /// Defaults to 10.
        /// </summary>
        public ushort Prefetch { get; set; } = 10;

        /// <summary>
        /// Gets or sets the delivery limit before a message is dead-lettered. Defaults to 8.
        /// Enforced by the broker through the quorum queue delivery limit, so the count
        /// holds across restarts and competing consumers.
        /// </summary>
        public int MaxAttempts { get; set; } = 8;

        /// <summary>
        /// Gets or sets the pause before a failed message is returned to the queue.
        /// Defaults to 5 seconds. The pause throttles hot redelivery loops; it briefly
        /// holds the consumer, which is the deliberate trade for a broker-side retry.
        /// </summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    }
}
