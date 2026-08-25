using Truss.Domain;

namespace Truss.Application
{
    /// <summary>
    /// Handles a domain event raised by an aggregate.
    /// Handlers run inside the unit of work, before changes are persisted,
    /// so any state they modify is committed atomically with the command.
    /// </summary>
    /// <typeparam name="TEvent">The type of the domain event.</typeparam>
    public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
    {
        /// <summary>
        /// Handles the domain event.
        /// </summary>
        /// <param name="domainEvent">The domain event to handle.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
    }
}
