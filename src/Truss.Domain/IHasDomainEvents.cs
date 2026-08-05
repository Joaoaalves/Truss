namespace Truss.Domain
{
    /// <summary>
    /// Exposes the domain events raised by an entity.
    /// Used by the infrastructure to collect and dispatch events without knowing the entity type.
    /// </summary>
    public interface IHasDomainEvents
    {
        /// <summary>
        /// Gets a read-only collection of domain events that have been raised by this entity.
        /// </summary>
        IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

        /// <summary>
        /// Clears all domain events associated with this entity.
        /// This is typically called after the events have been dispatched.
        /// </summary>
        void ClearDomainEvents();
    }
}
