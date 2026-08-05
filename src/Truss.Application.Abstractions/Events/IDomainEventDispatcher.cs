using Truss.Domain.Events;

namespace Truss.Application.Abstractions.Events
{
    /// <summary>
    /// Dispatches domain events to their registered handlers.
    /// </summary>
    public interface IDomainEventDispatcher
    {
        /// <summary>
        /// Dispatches each domain event to all of its registered handlers.
        /// </summary>
        /// <param name="domainEvents">The domain events to dispatch.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Dispatch(IReadOnlyCollection<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
    }
}
