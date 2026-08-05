using Truss.Domain.Rules;

namespace Truss.Domain.Tests.Fakes
{
    public class FakeBusinessRule(bool isBroken, string message = "Rule was broken") : IBusinessRule
    {
        public bool IsBroken() => isBroken;

        public string Message => message;
    }
}
