using System.Diagnostics;
using System.Diagnostics.Metrics;
using Truss.Application;

namespace Truss.Observability
{
    /// <summary>
    /// Pipeline behavior that emits a span per request through the "Truss.Application"
    /// activity source and records request metrics through the "Truss" meter.
    /// Both are BCL primitives: without a listener, such as the OpenTelemetry SDK,
    /// the cost is negligible and nothing is exported.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public class TracingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private static readonly ActivitySource Source = new("Truss.Application");
        private static readonly Meter Meter = new("Truss");

        private static readonly Counter<long> Requests =
            Meter.CreateCounter<long>("truss.requests", description: "Requests dispatched through Truss.");

        private static readonly Histogram<double> Duration =
            Meter.CreateHistogram<double>("truss.request.duration", unit: "ms", description: "Request duration in milliseconds.");

        /// <inheritdoc />
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var kind = request is ICommand<TResponse> ? "command" : "query";

            using var activity = Source.StartActivity(requestName);
            activity?.SetTag("truss.request", requestName);
            activity?.SetTag("truss.request.kind", kind);

            var start = Stopwatch.GetTimestamp();

            try
            {
                var response = await next();

                Record(requestName, kind, "success", start);
                return response;
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.Message);

                Record(requestName, kind, exception is RequestValidationException ? "rejected" : "failure", start);
                throw;
            }
        }

        private static void Record(string requestName, string kind, string outcome, long start)
        {
            var tags = new TagList
            {
                { "truss.request", requestName },
                { "truss.request.kind", kind },
                { "truss.outcome", outcome }
            };

            Requests.Add(1, tags);
            Duration.Record(Stopwatch.GetElapsedTime(start).TotalMilliseconds, tags);
        }
    }
}
