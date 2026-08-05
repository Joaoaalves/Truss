using FluentValidation;

namespace Truss.Application.Tests.Fakes
{
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
}
