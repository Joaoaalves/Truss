using FluentValidation;
using MediatR;
using Truss.Application;

namespace Truss.Benchmarks
{
    // The same trivial operation, spelled in each contender's dialect, so the
    // benchmark measures dispatch overhead and nothing else.

    public sealed record TrussPing(string Value) : Truss.Application.ICommand<string>;

    public class TrussPingHandler : ICommandHandler<TrussPing, string>
    {
        public Task<string> Handle(TrussPing request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Value);
        }
    }

    public sealed record TrussValidatedPing(string Value) : Truss.Application.ICommand<string>;

    public class TrussValidatedPingHandler : ICommandHandler<TrussValidatedPing, string>
    {
        public Task<string> Handle(TrussValidatedPing request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Value);
        }
    }

    public class TrussValidatedPingValidator : AbstractValidator<TrussValidatedPing>
    {
        public TrussValidatedPingValidator()
        {
            RuleFor(command => command.Value).NotEmpty();
        }
    }

    public sealed record MediatorPing(string Value) : MediatR.IRequest<string>;

    public class MediatorPingHandler : MediatR.IRequestHandler<MediatorPing, string>
    {
        public Task<string> Handle(MediatorPing request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Value);
        }
    }

    public sealed record MediatorValidatedPing(string Value) : MediatR.IRequest<string>;

    public class MediatorValidatedPingHandler : MediatR.IRequestHandler<MediatorValidatedPing, string>
    {
        public Task<string> Handle(MediatorValidatedPing request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request.Value);
        }
    }

    public class MediatorValidatedPingValidator : AbstractValidator<MediatorValidatedPing>
    {
        public MediatorValidatedPingValidator()
        {
            RuleFor(command => command.Value).NotEmpty();
        }
    }

    /// <summary>
    /// The FluentValidation behavior everyone pairs MediatR with, written the
    /// way Truss's own is, so both pipelines carry the same passenger.
    /// </summary>
    public class MediatorValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
        : MediatR.IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        public Task<TResponse> Handle(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            if (validators is ICollection<IValidator<TRequest>> { Count: 0 } || !validators.Any())
                return next();

            return Validate(request, next, cancellationToken);
        }

        private async Task<TResponse> Validate(TRequest request, MediatR.RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var results = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)));

            var failures = results.SelectMany(result => result.Errors).Where(failure => failure is not null).ToList();

            if (failures.Count != 0)
                throw new ValidationException(failures);

            return await next();
        }
    }
}
