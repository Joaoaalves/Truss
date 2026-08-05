using Microsoft.Extensions.DependencyInjection;
using Truss.Application.Abstractions.Events;
using Truss.Domain.Events;

namespace Truss.Application.Events
{
    internal abstract class DomainEventHandlerWrapper
    {
        public abstract Task Handle(
            IDomainEvent domainEvent,
            IServiceProvider provider,
            CancellationToken cancellationToken);
    }

    internal sealed class DomainEventHandlerWrapperImpl<TEvent> : DomainEventHandlerWrapper
        where TEvent : IDomainEvent
    {
        public override async Task Handle(
            IDomainEvent domainEvent,
            IServiceProvider provider,
            CancellationToken cancellationToken)
        {
            foreach (var handler in provider.GetServices<IDomainEventHandler<TEvent>>())
            {
                await handler.Handle((TEvent)domainEvent, cancellationToken);
            }
        }
    }
}
