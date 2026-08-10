namespace Truss.Domain
{
    /// <summary>
    /// Checks business rules from outside a domain type. Aggregates and value
    /// objects use their own protected CheckRule; a handler that can only reach
    /// the answer through I/O (uniqueness, existence in another system) checks
    /// the rule here, so the failure is the same 422 with the same code.
    /// </summary>
    public static class BusinessRule
    {
        /// <summary>
        /// Throws when the rule is broken.
        /// </summary>
        /// <param name="rule">The rule to check.</param>
        /// <exception cref="BusinessRuleValidationException">Thrown when the rule is broken.</exception>
        public static void Check(IBusinessRule rule)
        {
            ArgumentNullException.ThrowIfNull(rule);

            if (rule.IsBroken())
                throw new BusinessRuleValidationException(rule);
        }
    }
}
