using Truss.Auth;
using Xunit;

namespace Truss.Auth.Jwt.Tests
{
    public class OneTimeTokenServiceTests
    {
        private readonly OneTimeTokenService _tokens = new(TimeProvider.System);

        [Fact]
        public void Create_ProducesAHashableToken_WithTheLifetime()
        {
            var before = DateTimeOffset.UtcNow;
            var token = _tokens.Create(TimeSpan.FromHours(1));

            Assert.Equal(64, token.Token.Length);
            Assert.Equal(token.TokenHash, _tokens.Hash(token.Token));
            Assert.NotEqual(token.Token, token.TokenHash);
            Assert.InRange(token.ExpiresOn, before.AddHours(1).AddMinutes(-1), before.AddHours(1).AddMinutes(1));
        }

        [Fact]
        public void CreateCode_ProducesSixDigits()
        {
            for (var i = 0; i < 50; i++)
            {
                var code = _tokens.CreateCode(TimeSpan.FromMinutes(5));

                Assert.Equal(6, code.Token.Length);
                Assert.All(code.Token, character => Assert.True(char.IsAsciiDigit(character)));
                Assert.Equal(code.TokenHash, _tokens.Hash(code.Token));
            }
        }

        [Fact]
        public void Create_NeverRepeats()
        {
            var seen = new HashSet<string>();

            for (var i = 0; i < 100; i++)
                Assert.True(seen.Add(_tokens.Create(TimeSpan.FromMinutes(1)).Token));
        }
    }
}
