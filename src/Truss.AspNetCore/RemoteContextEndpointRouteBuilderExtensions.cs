using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Truss.Application;
using Truss.AspNetCore;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Serves a context's query contract to other services, the receiving half
    /// of AddRemoteContext.
    /// </summary>
    public static class RemoteContextEndpointRouteBuilderExtensions
    {
        private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;

        /// <summary>
        /// Maps every IQuery of the contracts assembly under the given prefix:
        /// POST {prefix}/{query full name} binds the JSON body to the query,
        /// dispatches it through the local pipeline and returns the result.
        /// Validation and business rule failures travel as the same
        /// ProblemDetails every endpoint produces, so the calling side can
        /// rethrow them as local outcomes. Only the declared contract is
        /// reachable; nothing is served by convention.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="contracts">The assembly holding the context's contract queries.</param>
        /// <param name="prefix">The route prefix. Defaults to "/truss/remote".</param>
        /// <returns>A group builder that can be used to further customize the endpoints.</returns>
        public static RouteGroupBuilder MapRemoteContext(this IEndpointRouteBuilder endpoints, Assembly contracts, string prefix = "/truss/remote")
        {
            ArgumentNullException.ThrowIfNull(contracts);

            var queries = new Dictionary<string, RemoteQueryInvoker>(StringComparer.Ordinal);

            foreach (var type in contracts.GetTypes().Where(type => type.IsClass && !type.IsAbstract && !type.IsGenericType))
            {
                foreach (var contract in type.GetInterfaces())
                {
                    if (!contract.IsGenericType || contract.GetGenericTypeDefinition() != typeof(IQuery<>))
                        continue;

                    queries[type.FullName!] = (RemoteQueryInvoker)Activator.CreateInstance(
                        typeof(RemoteQueryInvoker<,>).MakeGenericType(type, contract.GetGenericArguments()[0]))!;
                }
            }

            var group = endpoints.MapGroup(prefix);

            group.MapPost("/{query}", async (string query, HttpRequest request, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                {
                    if (!queries.TryGetValue(query, out var invoker))
                        return Results.NotFound();

                    var instance = await JsonSerializer.DeserializeAsync(request.Body, invoker.QueryType, Json, cancellationToken);

                    if (instance is null)
                        return Results.BadRequest();

                    return Results.Json(await invoker.Invoke(dispatcher, instance, cancellationToken), Json);
                })
                .AddTrussErrorHandling();

            return group;
        }

        private abstract class RemoteQueryInvoker
        {
            public abstract Type QueryType { get; }

            public abstract Task<object?> Invoke(IDispatcher dispatcher, object query, CancellationToken cancellationToken);
        }

        private sealed class RemoteQueryInvoker<TQuery, TResult> : RemoteQueryInvoker
            where TQuery : IRequest<TResult>
        {
            public override Type QueryType => typeof(TQuery);

            public override async Task<object?> Invoke(IDispatcher dispatcher, object query, CancellationToken cancellationToken)
            {
                return await dispatcher.Send((TQuery)query, cancellationToken);
            }
        }
    }
}
