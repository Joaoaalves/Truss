using System.Security.Claims;

namespace Truss.Auth
{
    /// <summary>
    /// The material of a newly created refresh token.
    /// The token goes to the client; only the hash is stored, so a database leak
    /// does not expose usable refresh tokens.
    /// </summary>
    /// <param name="Token">The opaque token handed to the client.</param>
    /// <param name="TokenHash">The hash to persist.</param>
    /// <param name="ExpiresOn">When the token expires.</param>
    public sealed record RefreshTokenMaterial(string Token, string TokenHash, DateTimeOffset ExpiresOn);

    /// <summary>
    /// Issues access and refresh tokens.
    /// The service knows nothing about the user model; identity travels as claims.
    /// </summary>
    public interface IJwtTokenService
    {
        /// <summary>
        /// Creates a signed access token carrying the given claims.
        /// </summary>
        /// <param name="claims">The claims to embed.</param>
        string CreateAccessToken(IEnumerable<Claim> claims);

        /// <summary>
        /// Creates a new opaque refresh token and the hash to store for it.
        /// </summary>
        RefreshTokenMaterial CreateRefreshToken();

        /// <summary>
        /// Computes the storage hash of a refresh token presented by a client.
        /// </summary>
        /// <param name="token">The token presented by the client.</param>
        string HashRefreshToken(string token);
    }
}
