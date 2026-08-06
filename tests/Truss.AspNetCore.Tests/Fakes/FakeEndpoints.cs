using FluentValidation;
using Truss.Application;
using Truss.Domain;

namespace Truss.AspNetCore.Tests.Fakes
{
    public sealed record PingCommand(string Value) : ICommand<string>;

    public class PingCommandHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> Handle(PingCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"pong:{request.Value}");
        }
    }

    public class PingCommandValueValidator : AbstractValidator<PingCommand>
    {
        public PingCommandValueValidator()
        {
            RuleFor(command => command.Value).NotEmpty();
        }
    }

    public class PingCommandLengthValidator : AbstractValidator<PingCommand>
    {
        public PingCommandLengthValidator()
        {
            RuleFor(command => command.Value).MinimumLength(3);
        }
    }

    public sealed record ArchiveCommand : ICommand;

    public class ArchiveCommandHandler : ICommandHandler<ArchiveCommand>
    {
        public Task<Unit> Handle(ArchiveCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Unit.Value);
        }
    }

    public sealed record CreateItemCommand(string Name) : ICommand<Guid>;

    public class CreateItemCommandHandler : ICommandHandler<CreateItemCommand, Guid>
    {
        public Task<Guid> Handle(CreateItemCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Guid.NewGuid());
        }
    }

    public class AlwaysBrokenRule : IBusinessRule
    {
        public bool IsBroken() => true;

        public string Message => "The item is locked.";
    }

    public sealed record BreakRuleCommand : ICommand;

    public class BreakRuleCommandHandler : ICommandHandler<BreakRuleCommand>
    {
        public Task<Unit> Handle(BreakRuleCommand request, CancellationToken cancellationToken)
        {
            throw new BusinessRuleValidationException(new AlwaysBrokenRule());
        }
    }

    public sealed record GetGreetingQuery(string Name) : IQuery<string>;

    public class GetGreetingQueryHandler : IQueryHandler<GetGreetingQuery, string>
    {
        public Task<string> Handle(GetGreetingQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"Hello {request.Name}");
        }
    }

    public sealed record GetItemQuery(Guid Id) : IQuery<Guid>;

    public class GetItemQueryHandler : IQueryHandler<GetItemQuery, Guid>
    {
        public Task<Guid> Handle(GetItemQuery request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Id);
        }
    }

    public sealed record ListNumbersQuery(int Page = 1, int Size = 2) : IQuery<PageResult<int>>;

    public class ListNumbersQueryHandler : IQueryHandler<ListNumbersQuery, PageResult<int>>
    {
        public Task<PageResult<int>> Handle(ListNumbersQuery request, CancellationToken cancellationToken)
        {
            var numbers = Enumerable.Range(1, 5).ToList();
            var page = new PageRequest(request.Page, request.Size);
            var items = numbers.Skip(page.Skip).Take(page.Size).ToList();

            return Task.FromResult(new PageResult<int>(items, page.Page, page.Size, numbers.Count));
        }
    }
}
