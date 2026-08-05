namespace Truss.Application.Abstractions.Commands
{
    /// <summary>
    /// Represents a void response for commands that do not produce a result.
    /// </summary>
    public readonly record struct Unit
    {
        /// <summary>
        /// Gets the single value of the <see cref="Unit"/> type.
        /// </summary>
        public static readonly Unit Value = default;
    }
}
