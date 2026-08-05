using Truss.Application.Abstractions.Commands;
using Truss.Application.Abstractions.Queries;

namespace Truss.Application.Tests.Fakes
{
    public sealed record PingCommand(string Value) : ICommand<string>;

    public class PingCommandHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> Handle(PingCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"pong:{request.Value}");
        }
    }

    public sealed record VoidCommand : ICommand;

    public class VoidCommandHandler : ICommandHandler<VoidCommand>
    {
        public Task<Unit> Handle(VoidCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Unit.Value);
        }
    }

    public sealed record ThrowingCommand : ICommand;

    public class ThrowingCommandHandler : ICommandHandler<ThrowingCommand>
    {
        public Task<Unit> Handle(ThrowingCommand request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Handler failed.");
        }
    }

    public sealed record OrphanCommand : ICommand<string>;

    public sealed record GetPingQuery : IQuery<string>;

    public class GetPingQueryHandler : IQueryHandler<GetPingQuery, string>
    {
        public Task<string> Handle(GetPingQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult("ping");
        }
    }
}
