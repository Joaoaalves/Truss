using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Truss.Application;
using Truss.AspNetCore;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Provides endpoint mapping for Truss commands and queries.
    /// Routes are always explicit; nothing is inferred from type names.
    /// Validation and business rule failures are converted to ProblemDetails responses automatically.
    /// </summary>
    public static class TrussEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Maps a POST endpoint that binds the request body to the command, dispatches it
        /// and returns 204 No Content on success.
        /// </summary>
        /// <typeparam name="TCommand">The command type bound from the request body.</typeparam>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="pattern">The route pattern.</param>
        /// <returns>A builder that can be used to further customize the endpoint.</returns>
        public static RouteHandlerBuilder MapCommand<TCommand>(this IEndpointRouteBuilder endpoints, string pattern)
            where TCommand : ICommand
        {
            return endpoints
                .MapPost(pattern, async (TCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                {
                    await dispatcher.Send(command, cancellationToken);
                    return Results.NoContent();
                })
                .AddTrussErrorHandling()
                .Produces(StatusCodes.Status204NoContent);
        }

        /// <summary>
        /// Maps a POST endpoint that binds the request body to the command, dispatches it
        /// and returns 200 OK with the command result.
        /// </summary>
        /// <typeparam name="TCommand">The command type bound from the request body.</typeparam>
        /// <typeparam name="TResult">The result type returned by the command.</typeparam>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="pattern">The route pattern.</param>
        /// <returns>A builder that can be used to further customize the endpoint.</returns>
        public static RouteHandlerBuilder MapCommand<TCommand, TResult>(this IEndpointRouteBuilder endpoints, string pattern)
            where TCommand : ICommand<TResult>
        {
            return endpoints
                .MapPost(pattern, async (TCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                {
                    var result = await dispatcher.Send(command, cancellationToken);
                    return Results.Ok(result);
                })
                .AddTrussErrorHandling()
                .Produces<TResult>(StatusCodes.Status200OK);
        }

        /// <summary>
        /// Maps a POST endpoint that binds the request body to the command, dispatches it
        /// and returns 201 Created with the command result and a Location header.
        /// </summary>
        /// <typeparam name="TCommand">The command type bound from the request body.</typeparam>
        /// <typeparam name="TResult">The result type returned by the command.</typeparam>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="pattern">The route pattern.</param>
        /// <param name="createdAtLocation">Builds the Location header value from the command result.</param>
        /// <returns>A builder that can be used to further customize the endpoint.</returns>
        public static RouteHandlerBuilder MapCommand<TCommand, TResult>(
            this IEndpointRouteBuilder endpoints,
            string pattern,
            Func<TResult, string> createdAtLocation)
            where TCommand : ICommand<TResult>
        {
            ArgumentNullException.ThrowIfNull(createdAtLocation);

            return endpoints
                .MapPost(pattern, async (TCommand command, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                {
                    var result = await dispatcher.Send(command, cancellationToken);
                    return Results.Created(createdAtLocation(result), result);
                })
                .AddTrussErrorHandling()
                .Produces<TResult>(StatusCodes.Status201Created);
        }

        /// <summary>
        /// Maps a GET endpoint that binds route values and the query string to the query,
        /// dispatches it and returns 200 OK with the result.
        /// </summary>
        /// <typeparam name="TQuery">The query type bound from route values and the query string.</typeparam>
        /// <typeparam name="TResult">The result type returned by the query.</typeparam>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="pattern">The route pattern.</param>
        /// <returns>A builder that can be used to further customize the endpoint.</returns>
        public static RouteHandlerBuilder MapQuery<TQuery, TResult>(this IEndpointRouteBuilder endpoints, string pattern)
            where TQuery : IQuery<TResult>
        {
            return endpoints
                .MapGet(pattern, async ([AsParameters] TQuery query, IDispatcher dispatcher, CancellationToken cancellationToken) =>
                {
                    var result = await dispatcher.Send(query, cancellationToken);

                    // A query declared as IQuery<T?> answers "not found" with null,
                    // so the endpoint says 404 instead of 200 with an empty body.
                    return result is null ? Results.NotFound() : Results.Ok(result);
                })
                .AddTrussErrorHandling()
                .Produces<TResult>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);
        }

        /// <summary>
        /// Adds the Truss exception filter and the ProblemDetails response metadata shared by all endpoints.
        /// Endpoints mapped by hand (any verb MapCommand does not cover) get the same
        /// translation of validation and business rule failures by calling this.
        /// </summary>
        /// <param name="builder">The endpoint builder to decorate.</param>
        /// <returns>The same builder, for chaining.</returns>
        public static RouteHandlerBuilder AddTrussErrorHandling(this RouteHandlerBuilder builder)
        {
            return builder
                .AddEndpointFilter(TrussExceptionFilter.Instance)
                .ProducesValidationProblem()
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        }
    }
}
