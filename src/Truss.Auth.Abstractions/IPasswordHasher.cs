namespace Truss.Auth
{
    /// <summary>
    /// Hashes and verifies passwords.
    /// The hash embeds its own parameters, so the algorithm can evolve without breaking stored hashes.
    /// </summary>
    public interface IPasswordHasher
    {
        /// <summary>
        /// Hashes a password with a fresh random salt.
        /// </summary>
        /// <param name="password">The plain text password.</param>
        /// <returns>The self-describing hash to store.</returns>
        string Hash(string password);

        /// <summary>
        /// Verifies a password against a stored hash in constant time.
        /// </summary>
        /// <param name="password">The plain text password.</param>
        /// <param name="hash">The stored hash.</param>
        /// <returns><c>true</c> when the password matches; otherwise, <c>false</c>.</returns>
        bool Verify(string password, string hash);
    }
}
