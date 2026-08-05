namespace Truss.Domain.Common
{
    /// <summary>
    /// Represents the base class for aggregate roots.
    /// Aggregate roots own their consistency boundary and are the only entities loaded and persisted directly.
    /// </summary>
    /// <typeparam name="TId">The type of the aggregate root identifier.</typeparam>
    public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
        where TId : notnull
    {
        /// <summary>
        /// Initializes a new instance of the aggregate root without an identifier.
        /// Intended for ORM materialization only.
        /// </summary>
        protected AggregateRoot()
        {
        }

        /// <summary>
        /// Initializes a new instance of the aggregate root with the given identifier.
        /// </summary>
        /// <param name="id">The identifier of the aggregate root.</param>
        protected AggregateRoot(TId id) : base(id)
        {
        }
    }
}
