using FluentValidation;

namespace Truss.Application
{
    /// <summary>
    /// Pipeline behavior that validates a request before it reaches the handler.
    /// Collects every failure from all registered validators and throws a single
    /// <see cref="RequestValidationException"/> containing all of them.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators = validators;

        /// <inheritdoc />
        public Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            // A request without validators pays nothing: no enumerator, no
            // async state machine, straight to the rest of the pipeline.
            if (_validators is ICollection<IValidator<TRequest>> { Count: 0 } || !_validators.Any())
                return next();

            return Validate(request, next, cancellationToken);
        }

        private async Task<TResponse> Validate(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var results = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken))
            );

            var errors = results
                .SelectMany(result => result.Errors)
                .Where(failure => failure is not null)
                .Select(failure => new ValidationError(failure.PropertyName, failure.ErrorMessage))
                .ToList();

            if (errors.Count != 0)
                throw new RequestValidationException(errors);

            return await next();
        }
    }
}
