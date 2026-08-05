namespace Truss.Domain
{
    /// <summary>
    /// Represents a base class for value objects.
    /// Value objects are immutable and compared by the values of their equality components.
    /// </summary>
    public abstract class ValueObject : IEquatable<ValueObject>
    {
        /// <summary>
        /// Returns the components that define the identity of the value object.
        /// Two value objects are equal when their components are equal and in the same order.
        /// </summary>
        /// <returns>The ordered sequence of equality components.</returns>
        protected abstract IEnumerable<object?> GetEqualityComponents();

        /// <inheritdoc />
        public bool Equals(ValueObject? other)
        {
            if (other is null)
                return false;

            if (GetType() != other.GetType())
                return false;

            return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is ValueObject other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            var hash = new HashCode();

            foreach (var component in GetEqualityComponents())
            {
                hash.Add(component);
            }

            return hash.ToHashCode();
        }

        /// <summary>
        /// Equality operator for value objects.
        /// </summary>
        public static bool operator ==(ValueObject? left, ValueObject? right)
        {
            if (left is null)
                return right is null;

            return left.Equals(right);
        }

        /// <summary>
        /// Inequality operator for value objects.
        /// </summary>
        public static bool operator !=(ValueObject? left, ValueObject? right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Validates a business rule and throws a <see cref="BusinessRuleValidationException"/> if the rule is broken.
        /// </summary>
        /// <param name="rule">The business rule to validate.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown if the rule is broken.</exception>
        protected static void CheckRule(IBusinessRule rule)
        {
            if (rule.IsBroken())
            {
                throw new BusinessRuleValidationException(rule);
            }
        }
    }
}
