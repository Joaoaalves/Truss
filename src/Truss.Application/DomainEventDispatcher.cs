using System.Collections.Concurrent;
using Truss.Domain;

namespace Truss.Application
{
    /// <summary>
    /// Default domain event dispatcher implementation.
    /// Resolves handlers from the current dependency injection scope,
    /// caching a typed invoker per event type so reflection happens only once.
    /// </summary>
    public class DomainEventDispatcher(IServiceProvider provider) : IDomainEventDispatcher
    {
        private static readonly ConcurrentDictionary<Type, DomainEventHandlerWrapper> Wrappers = new();

        private readonly IServiceProvider _provider = provider;

        /// <inheritdoc />
        public async Task Dispatch(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(domainEvents);

            foreach (var domainEvent in domainEvents)
            {
                var wrapper = Wrappers.GetOrAdd(
                    domainEvent.GetType(),
                    static eventType => (DomainEventHandlerWrapper)Activator.CreateInstance(
                        typeof(DomainEventHandlerWrapperImpl<>).MakeGenericType(eventType))!
                );

                await wrapper.Handle(domainEvent, _provider, cancellationToken);
            }
        }
    }
}
