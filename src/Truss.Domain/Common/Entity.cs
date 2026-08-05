using Truss.Domain.Events;
using Truss.Domain.Rules;

namespace Truss.Domain.Common
{
    /// <summary>
    /// Represents the base class for all entities in the domain layer.
    /// Entities are compared by identity and provide domain event handling and business rule validation.
    /// </summary>
    /// <typeparam name="TId">The type of the entity identifier.</typeparam>
    public abstract class Entity<TId> : IHasDomainEvents, IEquatable<Entity<TId>>
        where TId : notnull
    {
        private List<IDomainEvent>? _domainEvents;

        /// <summary>
        /// Initializes a new instance of the entity without an identifier.
        /// Intended for ORM materialization only.
        /// </summary>
        protected Entity()
        {
            Id = default!;
        }

        /// <summary>
        /// Initializes a new instance of the entity with the given identifier.
        /// </summary>
        /// <param name="id">The identifier of the entity.</param>
        protected Entity(TId id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets the identifier of the entity.
        /// </summary>
        public TId Id { get; protected set; }

        /// <inheritdoc />
        public IReadOnlyCollection<IDomainEvent> DomainEvents =>
            _domainEvents?.AsReadOnly() ?? (IReadOnlyCollection<IDomainEvent>)Array.Empty<IDomainEvent>();

        /// <summary>
        /// Adds a domain event to the entity's list of events.
        /// This should be called whenever a significant change occurs in the entity that other parts of the system should react to.
        /// </summary>
        /// <param name="domainEvent">The domain event to add.</param>
        protected void AddDomainEvent(IDomainEvent domainEvent)
        {
            _domainEvents ??= [];
            _domainEvents.Add(domainEvent);
        }

        /// <inheritdoc />
        public void ClearDomainEvents()
        {
            _domainEvents?.Clear();
        }

        /// <summary>
        /// Checks a business rule and throws a <see cref="BusinessRuleValidationException"/> if the rule is broken.
        /// Use this to enforce domain invariants before performing operations.
        /// </summary>
        /// <param name="rule">The business rule to validate.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when the business rule is broken.</exception>
        protected static void CheckRule(IBusinessRule rule)
        {
            if (rule.IsBroken())
            {
                throw new BusinessRuleValidationException(rule);
            }
        }

        /// <summary>
        /// Determines whether the entity has no identifier assigned yet.
        /// </summary>
        private bool IsTransient()
        {
            return EqualityComparer<TId>.Default.Equals(Id, default!);
        }

        /// <inheritdoc />
        public bool Equals(Entity<TId>? other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (GetType() != other.GetType())
                return false;

            if (IsTransient() || other.IsTransient())
                return false;

            return EqualityComparer<TId>.Default.Equals(Id, other.Id);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is Entity<TId> other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return IsTransient()
                ? base.GetHashCode()
                : HashCode.Combine(GetType(), Id);
        }

        /// <summary>
        /// Checks equality between two entities.
        /// </summary>
        public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        /// <summary>
        /// Checks inequality between two entities.
        /// </summary>
        public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
        {
            return !(left == right);
        }
    }
}
