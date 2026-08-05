namespace Truss.Application.Abstractions.Validation
{
    /// <summary>
    /// Exception thrown when a request fails validation.
    /// Contains every validation failure found, not only the first one.
    /// </summary>
    public class RequestValidationException(IReadOnlyList<ValidationError> errors)
        : Exception("One or more validation failures occurred.")
    {
        /// <summary>
        /// Gets the validation failures that caused the exception.
        /// </summary>
        public IReadOnlyList<ValidationError> Errors { get; } = errors;
    }
}
