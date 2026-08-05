using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Truss.Auth;
using Xunit;

namespace Truss.Auth.Jwt.Tests
{
    public class JwtTokenServiceTests
    {
        private static readonly TrussJwtOptions Options = new()
        {
            Issuer = "truss-tests",
            Audience = "truss-tests",
            SigningKey = new string('k', 48)
        };

        private readonly JwtTokenService _service = new(
            Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System);

        [Fact]
        public async Task CreateAccessToken_ProducesValidatableToken_WithClaims()
        {
            var userId = Guid.NewGuid().ToString();

            var token = _service.CreateAccessToken([new Claim("sub", userId), new Claim("email", "joao@example.com")]);

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidIssuer = Options.Issuer,
                ValidAudience = Options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.SigningKey))
            });

            Assert.True(result.IsValid);
            Assert.Equal(userId, result.Claims["sub"]);
        }

        [Fact]
        public async Task CreateAccessToken_IsRejectedByDifferentKey()
        {
            var token = _service.CreateAccessToken([new Claim("sub", "x")]);

            var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidIssuer = Options.Issuer,
                ValidAudience = Options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 48)))
            });

            Assert.False(result.IsValid);
        }

        [Fact]
        public void CreateRefreshToken_HashMatchesToken_AndExpiresInTheFuture()
        {
            var material = _service.CreateRefreshToken();

            Assert.NotEqual(material.Token, material.TokenHash);
            Assert.Equal(material.TokenHash, _service.HashRefreshToken(material.Token));
            Assert.True(material.ExpiresOn > DateTimeOffset.UtcNow.AddDays(6));
        }

        [Fact]
        public void Constructor_RejectsShortSigningKey()
        {
            var invalid = new TrussJwtOptions { Issuer = "i", Audience = "a", SigningKey = "short" };

            Assert.Throws<InvalidOperationException>(() =>
                new JwtTokenService(Microsoft.Extensions.Options.Options.Create(invalid), TimeProvider.System));
        }
    }
}
