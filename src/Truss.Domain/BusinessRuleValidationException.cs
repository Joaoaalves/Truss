namespace Truss.Domain
{
    /// <summary>
    /// Exception thrown when a business rule is violated.
    /// Contains the broken rule and a message describing the violation.
    /// </summary>
    public class BusinessRuleValidationException(IBusinessRule brokenRule) : Exception(brokenRule.Message)
    {
        /// <summary>
        /// Gets the broken business rule that caused the exception.
        /// </summary>
        public IBusinessRule BrokenRule { get; } = brokenRule;

        /// <summary>
        /// Returns a string that represents the current exception.
        /// Includes the type and message of the broken rule.
        /// </summary>
        /// <returns>A string representation of the exception.</returns>
        public override string ToString()
        {
            return $"{BrokenRule.GetType().FullName}: {BrokenRule.Message}";
        }
    }
}
