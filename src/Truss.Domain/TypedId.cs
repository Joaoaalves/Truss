namespace Truss.Domain
{
    /// <summary>
    /// Base record for strongly-typed identifiers.
    /// Provides value-based equality and a readable string representation.
    /// </summary>
    /// <typeparam name="TValue">The type of the underlying identifier value.</typeparam>
    /// <param name="Value">The underlying identifier value.</param>
    public abstract record TypedId<TValue>(TValue Value) where TValue : notnull
    {
        /// <summary>
        /// Returns the string representation of the underlying value.
        /// </summary>
        public sealed override string ToString()
        {
            return Value.ToString() ?? string.Empty;
        }
    }
}
