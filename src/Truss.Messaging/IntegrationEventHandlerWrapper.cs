using Microsoft.Extensions.DependencyInjection;

namespace Truss.Messaging
{
    internal abstract class IntegrationEventHandlerWrapper
    {
        public abstract Task Handle(
            IIntegrationEvent integrationEvent,
            IServiceProvider provider,
            CancellationToken cancellationToken);
    }

    internal sealed class IntegrationEventHandlerWrapperImpl<TEvent> : IntegrationEventHandlerWrapper
        where TEvent : IIntegrationEvent
    {
        public override async Task Handle(
            IIntegrationEvent integrationEvent,
            IServiceProvider provider,
            CancellationToken cancellationToken)
        {
            foreach (var handler in provider.GetServices<IIntegrationEventHandler<TEvent>>())
            {
                await handler.Handle((TEvent)integrationEvent, cancellationToken);
            }
        }
    }
}
