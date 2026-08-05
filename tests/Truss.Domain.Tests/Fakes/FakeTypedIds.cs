using Truss.Domain.Common;

namespace Truss.Domain.Tests.Fakes
{
    public sealed record FakeId(Guid Value) : TypedId<Guid>(Value);

    public sealed record OtherId(Guid Value) : TypedId<Guid>(Value);
}
