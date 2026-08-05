using Microsoft.Extensions.Logging;
using Truss.Application;

namespace Truss.Observability
{
    /// <summary>
    /// Pipeline behavior that logs every request with a structured scope:
    /// request name, correlation id, duration and outcome.
    /// When no correlation id is set, one is created for the duration of the dispatch,
    /// so everything below it, including domain event handlers, logs under the same id.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger = logger;

        /// <inheritdoc />
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            var correlationId = ExecutionContextHolder.Current ?? Guid.NewGuid();
            ExecutionContextHolder.Current = correlationId;

            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["RequestName"] = requestName,
                ["CorrelationId"] = correlationId
            });

            var start = System.Diagnostics.Stopwatch.GetTimestamp();

            try
            {
                var response = await next();

                _logger.LogInformation(
                    "Handled {RequestName} in {ElapsedMilliseconds}ms",
                    requestName,
                    Elapsed(start));

                return response;
            }
            catch (RequestValidationException exception)
            {
                _logger.LogWarning(
                    "Rejected {RequestName} with {FailureCount} validation failures after {ElapsedMilliseconds}ms",
                    requestName,
                    exception.Errors.Count,
                    Elapsed(start));

                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed {RequestName} after {ElapsedMilliseconds}ms",
                    requestName,
                    Elapsed(start));

                throw;
            }
        }

        private static double Elapsed(long start)
        {
            return Math.Round(System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds, 1);
        }
    }
}
