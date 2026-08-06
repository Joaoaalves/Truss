namespace Truss.Auth
{
    /// <summary>
    /// A single-use token: the value handed to the user and the hash that goes
    /// to storage. The value never touches the database.
    /// </summary>
    /// <param name="Token">The value delivered to the user.</param>
    /// <param name="TokenHash">The hash to store and compare.</param>
    /// <param name="ExpiresOn">When the token stops being acceptable.</param>
    public sealed record OneTimeToken(string Token, string TokenHash, DateTimeOffset ExpiresOn);

    /// <summary>
    /// Creates single-use tokens for account flows: opaque tokens for links
    /// (password reset, email confirmation) and short numeric codes a person
    /// can type from an email (two factor login).
    /// </summary>
    public interface IOneTimeTokens
    {
        /// <summary>
        /// Creates an opaque token for links.
        /// </summary>
        /// <param name="lifetime">How long the token stays acceptable.</param>
        OneTimeToken Create(TimeSpan lifetime);

        /// <summary>
        /// Creates a six digit numeric code.
        /// </summary>
        /// <param name="lifetime">How long the code stays acceptable.</param>
        OneTimeToken CreateCode(TimeSpan lifetime);

        /// <summary>
        /// Hashes a presented token for lookup against storage.
        /// </summary>
        /// <param name="token">The presented token.</param>
        string Hash(string token);
    }
}
