using System.Text.Json;

namespace Truss.Application.Pipeline
{
    /// <summary>
    /// Pipeline behavior that makes commands idempotent per client-supplied key.
    /// A replayed key returns the stored response without touching the handler.
    /// The behavior registers after the unit of work, so the record commits in the
    /// same transaction as the command: a command can never apply twice, and a
    /// command that fails leaves no record behind.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public sealed class IdempotencyBehavior<TRequest, TResponse>(
        IIdempotencyStore store,
        TimeProvider timeProvider) : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        /// <inheritdoc />
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (IdempotencyKeyHolder.Current is not { } key || request is not ICommand<TResponse>)
                return await next();

            // The request type scopes the key, so reusing a key on another
            // command never replays a foreign response.
            var storageKey = $"{typeof(TRequest).FullName}:{key}";

            if (await store.FindResponse(storageKey, cancellationToken) is { } stored)
                return JsonSerializer.Deserialize<TResponse>(stored)!;

            var response = await next();

            store.Add(storageKey, JsonSerializer.Serialize(response), timeProvider.GetUtcNow());

            return response;
        }
    }
}
