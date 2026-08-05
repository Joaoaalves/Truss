using FluentValidation;
using Truss.Application;
using Truss.Domain;

namespace Truss.Generators.Tests.Fakes
{
    public sealed record GenPingCommand(string Value) : ICommand<string>;

    public class GenPingCommandHandler : ICommandHandler<GenPingCommand, string>
    {
        public Task<string> Handle(GenPingCommand request, CancellationToken cancellationToken)
        {
            return Task.FromResult($"gen:{request.Value}");
        }
    }

    public class GenPingCommandValidator : AbstractValidator<GenPingCommand>
    {
        public GenPingCommandValidator()
        {
            RuleFor(command => command.Value).NotEmpty();
        }
    }

    public sealed record GenEvent(Guid Id) : DomainEvent;

    public class GenEventRecorder
    {
        public List<Guid> Handled { get; } = [];
    }

    public class GenEventHandler(GenEventRecorder recorder) : IDomainEventHandler<GenEvent>
    {
        public Task Handle(GenEvent domainEvent, CancellationToken cancellationToken)
        {
            recorder.Handled.Add(domainEvent.Id);
            return Task.CompletedTask;
        }
    }
}
