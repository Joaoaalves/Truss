using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Truss.Jobs;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Provides the job progress endpoints.
    /// </summary>
    public static class TrussJobsEndpointRouteBuilderExtensions
    {
        private static readonly JsonSerializerOptions Json = new(JsonSerializerOptions.Web)
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        /// <summary>
        /// Maps the job progress endpoints under the given prefix:
        /// GET {prefix}/{id} returns the current snapshot, and
        /// GET {prefix}/{id}/stream pushes snapshots over server-sent events until the job completes.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="prefix">The route prefix. Defaults to "/truss/jobs".</param>
        /// <param name="streamInterval">The interval between stream updates. Defaults to 1 second.</param>
        /// <returns>A group builder that can be used to further customize the endpoints.</returns>
        public static RouteGroupBuilder MapTrussJobs(
            this IEndpointRouteBuilder endpoints,
            string prefix = "/truss/jobs",
            TimeSpan? streamInterval = null)
        {
            var interval = streamInterval ?? TimeSpan.FromSeconds(1);
            var group = endpoints.MapGroup(prefix);

            group.MapGet("/{id:guid}", async (Guid id, IJobMonitor monitor, CancellationToken cancellationToken) =>
                await monitor.Get(id, cancellationToken) is { } snapshot
                    ? Results.Json(snapshot, Json)
                    : Results.NotFound())
                .Produces<JobSnapshot>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status404NotFound);

            group.MapGet("/{id:guid}/stream", async (Guid id, HttpContext http, IServiceScopeFactory scopeFactory, CancellationToken cancellationToken) =>
            {
                http.Response.Headers.ContentType = "text/event-stream";
                http.Response.Headers.CacheControl = "no-cache";

                while (!cancellationToken.IsCancellationRequested)
                {
                    JobSnapshot? snapshot;

                    await using (var scope = scopeFactory.CreateAsyncScope())
                    {
                        snapshot = await scope.ServiceProvider.GetRequiredService<IJobMonitor>().Get(id, cancellationToken);
                    }

                    if (snapshot is null)
                    {
                        await http.Response.WriteAsync("event: notfound\ndata: {}\n\n", cancellationToken);
                        await http.Response.Body.FlushAsync(cancellationToken);
                        return;
                    }

                    await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(snapshot, Json)}\n\n", cancellationToken);
                    await http.Response.Body.FlushAsync(cancellationToken);

                    if (snapshot.Status is JobStatus.Succeeded or JobStatus.Failed)
                        return;

                    await Task.Delay(interval, cancellationToken);
                }
            });

            return group;
        }
    }
}
