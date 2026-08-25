namespace Truss.Application
{
    /// <summary>
    /// Represents a single validation failure for a request property.
    /// </summary>
    /// <param name="PropertyName">The name of the property that failed validation.</param>
    /// <param name="Message">The message describing the failure.</param>
    public sealed record ValidationError(string PropertyName, string Message);
}
