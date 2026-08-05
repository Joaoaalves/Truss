using Truss.Domain.Common;

namespace Truss.Domain.Tests.Fakes
{
    public class FakeAddress(string street, string city) : ValueObject
    {
        public string Street { get; } = street;

        public string City { get; } = city;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Street;
            yield return City;
        }
    }

    public class OtherAddress(string street, string city) : ValueObject
    {
        public string Street { get; } = street;

        public string City { get; } = city;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Street;
            yield return City;
        }
    }
}
