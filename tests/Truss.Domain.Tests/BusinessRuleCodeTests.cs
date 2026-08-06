using Truss.Domain;
using Xunit;

namespace Truss.Domain.Tests
{
    public class BusinessRuleCodeTests
    {
        private sealed class StockMustBeAvailable : IBusinessRule
        {
            public bool IsBroken() => true;

            public string Message => "Out of stock.";
        }

        private sealed class RenamedRule : IBusinessRule
        {
            public bool IsBroken() => true;

            public string Message => "Renamed.";

            public string Code => "inventory.reserved";
        }

        [Fact]
        public void Code_DefaultsToTheTypeName()
        {
            IBusinessRule rule = new StockMustBeAvailable();

            Assert.Equal("StockMustBeAvailable", rule.Code);
        }

        [Fact]
        public void Code_CanBePinnedIndependentlyOfTheTypeName()
        {
            IBusinessRule rule = new RenamedRule();

            Assert.Equal("inventory.reserved", rule.Code);
        }
    }
}
