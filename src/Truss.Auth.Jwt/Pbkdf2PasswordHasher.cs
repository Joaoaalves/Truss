using System.Security.Cryptography;

namespace Truss.Auth
{
    /// <summary>
    /// PBKDF2 password hasher using SHA-256, a per-password random salt and a
    /// self-describing hash format, verified in constant time.
    /// Uses only the base class library.
    /// </summary>
    public sealed class Pbkdf2PasswordHasher : IPasswordHasher
    {
        private const string Prefix = "TRUSSV1";
        private const int Iterations = 210_000;
        private const int SaltSize = 16;
        private const int KeySize = 32;

        /// <inheritdoc />
        public string Hash(string password)
        {
            ArgumentNullException.ThrowIfNull(password);

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);

            return $"{Prefix}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
        }

        /// <inheritdoc />
        public bool Verify(string password, string hash)
        {
            ArgumentNullException.ThrowIfNull(password);
            ArgumentNullException.ThrowIfNull(hash);

            var parts = hash.Split('.');

            if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations))
                return false;

            try
            {
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);

                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
