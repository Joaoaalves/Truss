using RabbitMQ.Client;

namespace Truss.Messaging.RabbitMq
{
    internal static class RabbitMqTopology
    {
        public static string DeadLetterQueue(TrussRabbitMqTransportOptions options)
        {
            return options.QueueName + ".dead";
        }

        /// <summary>
        /// Declares the main and dead-letter queues. Both are durable quorum queues;
        /// the broker moves a message to the dead-letter queue once its delivery
        /// limit is exhausted, so retry accounting survives restarts.
        /// </summary>
        public static async Task Declare(IChannel channel, TrussRabbitMqTransportOptions options, CancellationToken cancellationToken)
        {
            var deadLetterQueue = DeadLetterQueue(options);

            await channel.QueueDeclareAsync(
                deadLetterQueue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-queue-type"] = "quorum"
                },
                cancellationToken: cancellationToken);

            await channel.QueueDeclareAsync(
                options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-queue-type"] = "quorum",
                    ["x-delivery-limit"] = Math.Max(0, options.MaxAttempts - 1),
                    ["x-dead-letter-exchange"] = string.Empty,
                    ["x-dead-letter-routing-key"] = deadLetterQueue
                },
                cancellationToken: cancellationToken);
        }
    }
}
