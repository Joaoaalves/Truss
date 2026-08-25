using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Truss.Messaging;
using Truss.Messaging.Outbox;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// The result of a dead-letter retry.
    /// </summary>
    /// <param name="Retried">How many messages were returned to the queue.</param>
    public sealed record OutboxRetryResult(int Retried);

    /// <summary>
    /// Provides the operational endpoints of the outbox.
    /// </summary>
    public static class TrussOutboxEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Maps the outbox endpoints under the given prefix:
        /// GET {prefix} returns the outbox counters, and
        /// POST {prefix}/retry returns every dead-lettered message to the queue
        /// and wakes the processor. These operate on your infrastructure, so
        /// protect them like any admin surface, for example with
        /// .RequireAuthorization on the returned group.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="prefix">The route prefix. Defaults to "/truss/outbox".</param>
        /// <returns>A group builder that can be used to further customize the endpoints.</returns>
        public static RouteGroupBuilder MapTrussOutbox(this IEndpointRouteBuilder endpoints, string prefix = "/truss/outbox")
        {
            var group = endpoints.MapGroup(prefix);

            group.MapGet("/", async (IOutboxStore store, CancellationToken cancellationToken) =>
                    Results.Ok(await store.GetStatistics(cancellationToken)))
                .Produces<OutboxStatistics>(StatusCodes.Status200OK);

            group.MapPost("/retry", async (IOutboxStore store, OutboxSignal signal, CancellationToken cancellationToken) =>
                {
                    var retried = await store.RetryDeadLettered(cancellationToken);

                    if (retried > 0)
                        signal.Notify();

                    return Results.Ok(new OutboxRetryResult(retried));
                })
                .Produces<OutboxRetryResult>(StatusCodes.Status200OK);

            return group;
        }
    }
}
