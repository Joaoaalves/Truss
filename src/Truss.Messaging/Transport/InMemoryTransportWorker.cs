using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Truss.Messaging.Dispatch;

namespace Truss.Messaging.Transport
{
    internal sealed class InMemoryTransportWorker(
        InMemoryTransport transport,
        IIntegrationEventDispatcher dispatcher,
        ILogger<InMemoryTransportWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var envelope in transport.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await dispatcher.Dispatch(envelope, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Failed to handle integration event {Name} v{Version} ({MessageId}).", envelope.Name, envelope.Version, envelope.MessageId);
                }
                finally
                {
                    transport.MarkDelivered();
                }
            }
        }
    }
}
