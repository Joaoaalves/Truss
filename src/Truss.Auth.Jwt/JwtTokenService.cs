using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Truss.Auth
{
    /// <summary>
    /// Default token service: HMAC-SHA256 signed access tokens and opaque random
    /// refresh tokens whose hashes are what gets stored.
    /// </summary>
    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly TrussJwtOptions _options;
        private readonly TimeProvider _timeProvider;

        /// <summary>
        /// Initializes the service with its options.
        /// </summary>
        /// <param name="options">The JWT options.</param>
        /// <param name="timeProvider">The time source.</param>
        public JwtTokenService(IOptions<TrussJwtOptions> options, TimeProvider timeProvider)
        {
            _options = options.Value;
            _timeProvider = timeProvider;
            _options.Validate();
        }

        /// <inheritdoc />
        public string CreateAccessToken(IEnumerable<Claim> claims)
        {
            ArgumentNullException.ThrowIfNull(claims);

            var handler = new JsonWebTokenHandler();
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            return handler.CreateToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = now,
                NotBefore = now,
                Expires = now + _options.AccessTokenLifetime,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                    SecurityAlgorithms.HmacSha256)
            });
        }

        /// <inheritdoc />
        public RefreshTokenMaterial CreateRefreshToken()
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(48));

            return new RefreshTokenMaterial(
                token,
                HashRefreshToken(token),
                _timeProvider.GetUtcNow() + _options.RefreshTokenLifetime);
        }

        /// <inheritdoc />
        public string HashRefreshToken(string token)
        {
            ArgumentNullException.ThrowIfNull(token);

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        }
    }
}
