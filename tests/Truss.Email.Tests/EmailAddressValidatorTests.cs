using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Truss.Email;
using Xunit;

namespace Truss.Email.Tests
{
    public class EmailAddressValidatorTests
    {
        private static EmailAddressValidator Offline()
        {
            return new EmailAddressValidator(Options.Create(new TrussEmailValidationOptions { VerifyMailServer = false }));
        }

        [Theory]
        [InlineData("joao@example.com")]
        [InlineData("joao.alves+tag@sub.example.co")]
        public async Task WellFormedAddresses_AreValid(string address)
        {
            var result = await Offline().Validate(address);

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-an-email")]
        [InlineData("a@b@c@d")]
        [InlineData("joao@localhost")]
        [InlineData("Joao Alves <joao@example.com>")]
        public async Task MalformedAddresses_AreRejected_WithAReason(string address)
        {
            var result = await Offline().Validate(address);

            Assert.False(result.IsValid);
            Assert.NotNull(result.Reason);
        }

        [Fact]
        public async Task DomainWithAMailServer_IsValid()
        {
            var validator = new EmailAddressValidator(Options.Create(new TrussEmailValidationOptions()));

            var result = await validator.Validate("someone@gmail.com");

            Assert.True(result.IsValid);
        }

        [Fact]
        public async Task DomainThatCannotExist_IsRejected()
        {
            var validator = new EmailAddressValidator(Options.Create(new TrussEmailValidationOptions()));

            var result = await validator.Validate("someone@no-mail-here.invalid");

            Assert.False(result.IsValid);
            Assert.Contains("no mail", result.Reason);
        }

        [Fact]
        public void Registration_ResolvesTheValidator()
        {
            var provider = new ServiceCollection()
                .AddTrussEmailValidation(options => options.VerifyMailServer = false)
                .BuildServiceProvider();

            Assert.IsType<EmailAddressValidator>(provider.GetRequiredService<IEmailAddressValidator>());
        }
    }
}
