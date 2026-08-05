using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Truss.Messaging.RabbitMq
{
    internal sealed class RabbitMqConsumer(
        RabbitMqTransport transport,
        IIntegrationEventDispatcher dispatcher,
        IOptions<TrussRabbitMqTransportOptions> options,
        ILogger<RabbitMqConsumer> logger) : BackgroundService
    {
        private readonly TrussRabbitMqTransportOptions _options = options.Value;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.EnableConsumer)
                return;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Consume(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "RabbitMQ consumer failed; reconnecting.");

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private async Task Consume(CancellationToken stoppingToken)
        {
            var connection = await transport.GetConnection();

            await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await RabbitMqTopology.Declare(channel, _options, stoppingToken);
            await channel.BasicQosAsync(0, _options.Prefetch, global: false, stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, delivery) => HandleDelivery(channel, delivery, stoppingToken);

            await channel.BasicConsumeAsync(
                queue: _options.QueueName,
                autoAck: false,
                consumerTag: string.Empty,
                noLocal: false,
                exclusive: false,
                arguments: null,
                consumer: consumer,
                cancellationToken: stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task HandleDelivery(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
        {
            var envelope = RabbitMqEnvelope.FromBody(delivery.Body);

            if (envelope is null)
            {
                logger.LogError("Delivery {DeliveryTag} is malformed; dead-lettering it.", delivery.DeliveryTag);

                await channel.BasicPublishAsync(
                    exchange: string.Empty,
                    routingKey: RabbitMqTopology.DeadLetterQueue(_options),
                    mandatory: false,
                    basicProperties: new BasicProperties { Persistent = true },
                    body: delivery.Body,
                    cancellationToken: cancellationToken);

                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
                return;
            }

            try
            {
                await dispatcher.Dispatch(envelope, cancellationToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The host is stopping; leaving the delivery unacknowledged
                // returns it to the queue when the channel closes.
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Message {MessageId} ({Name} v{Version}) failed; returning it to the queue.", envelope.MessageId, envelope.Name, envelope.Version);

                try
                {
                    await Task.Delay(_options.RetryDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                // basic.reject, not basic.nack: since RabbitMQ 4.3 only rejects count
                // toward the quorum queue delivery limit; nacked requeues loop forever.
                await channel.BasicRejectAsync(delivery.DeliveryTag, requeue: true, cancellationToken);
            }
        }
    }
}
