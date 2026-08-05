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
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (!_validators.Any())
                return await next();

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
