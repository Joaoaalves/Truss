namespace Truss.Email
{
    /// <summary>
    /// The outcome of validating an email address.
    /// </summary>
    /// <param name="IsValid">Whether the address is acceptable.</param>
    /// <param name="Reason">Why the address was rejected, when it was.</param>
    public sealed record EmailAddressValidation(bool IsValid, string? Reason)
    {
        /// <summary>
        /// The accepted outcome.
        /// </summary>
        public static readonly EmailAddressValidation Valid = new(true, null);

        /// <summary>
        /// Creates a rejected outcome with its reason.
        /// </summary>
        public static EmailAddressValidation Invalid(string reason) => new(false, reason);
    }

    /// <summary>
    /// Validates email addresses beyond the shape check a validator gives:
    /// real syntax and, when enabled, whether the domain accepts mail at all.
    /// </summary>
    public interface IEmailAddressValidator
    {
        /// <summary>
        /// Validates one address.
        /// </summary>
        /// <param name="address">The address to validate.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<EmailAddressValidation> Validate(string address, CancellationToken cancellationToken = default);
    }
}
