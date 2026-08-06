using System.Security.Cryptography;
using System.Text;

namespace Truss.Auth
{
    /// <summary>
    /// One-time tokens over base class library cryptography: random material,
    /// SHA-256 for storage, codes drawn with a uniform generator.
    /// </summary>
    public sealed class OneTimeTokenService(TimeProvider timeProvider) : IOneTimeTokens
    {
        /// <inheritdoc />
        public OneTimeToken Create(TimeSpan lifetime)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

            return new OneTimeToken(token, Hash(token), timeProvider.GetUtcNow() + lifetime);
        }

        /// <inheritdoc />
        public OneTimeToken CreateCode(TimeSpan lifetime)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

            return new OneTimeToken(code, Hash(code), timeProvider.GetUtcNow() + lifetime);
        }

        /// <inheritdoc />
        public string Hash(string token)
        {
            ArgumentNullException.ThrowIfNull(token);

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }
    }
}
