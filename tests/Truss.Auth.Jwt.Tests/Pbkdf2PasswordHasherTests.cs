using Truss.Auth;
using Xunit;

namespace Truss.Auth.Jwt.Tests
{
    public class Pbkdf2PasswordHasherTests
    {
        private readonly Pbkdf2PasswordHasher _hasher = new();

        [Fact]
        public void Verify_AcceptsCorrectPassword()
        {
            var hash = _hasher.Hash("correct horse battery staple");

            Assert.True(_hasher.Verify("correct horse battery staple", hash));
        }

        [Fact]
        public void Verify_RejectsWrongPassword()
        {
            var hash = _hasher.Hash("correct horse battery staple");

            Assert.False(_hasher.Verify("wrong horse", hash));
        }

        [Fact]
        public void Hash_UsesFreshSaltPerPassword()
        {
            var first = _hasher.Hash("same password");
            var second = _hasher.Hash("same password");

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void Verify_RejectsMalformedHash()
        {
            Assert.False(_hasher.Verify("password", "not-a-hash"));
            Assert.False(_hasher.Verify("password", "TRUSSV1.bad.salt.key"));
        }
    }
}
