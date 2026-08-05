using Truss.Domain;
using Truss.Domain.Tests.Fakes;
using Xunit;

namespace Truss.Domain.Tests
{
    public class BusinessRuleValidationExceptionTests
    {
        [Fact]
        public void Exception_ExposesBrokenRuleAndMessage()
        {
            var rule = new FakeBusinessRule(isBroken: true, message: "Quantity must be positive");

            var exception = new BusinessRuleValidationException(rule);

            Assert.Same(rule, exception.BrokenRule);
            Assert.Equal("Quantity must be positive", exception.Message);
        }

        [Fact]
        public void ToString_IncludesRuleTypeAndMessage()
        {
            var rule = new FakeBusinessRule(isBroken: true, message: "Quantity must be positive");

            var exception = new BusinessRuleValidationException(rule);

            Assert.Contains(rule.GetType().FullName!, exception.ToString());
            Assert.Contains("Quantity must be positive", exception.ToString());
        }
    }
}
