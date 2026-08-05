using Microsoft.AspNetCore.Http;
using Truss.Observability;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Provides the correlation middleware.
    /// </summary>
    public static class TrussCorrelationApplicationBuilderExtensions
    {
        /// <summary>
        /// Reads the correlation id from the request header, or creates one, makes it ambient
        /// for everything handled during the request and echoes it back in the response header.
        /// Place it early in the pipeline, before endpoints.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="headerName">The header carrying the correlation id. Defaults to "X-Correlation-Id".</param>
        /// <returns>The updated <see cref="IApplicationBuilder"/>.</returns>
        public static IApplicationBuilder UseTrussCorrelation(this IApplicationBuilder app, string headerName = "X-Correlation-Id")
        {
            ArgumentNullException.ThrowIfNull(headerName);

            return app.Use(async (context, next) =>
            {
                var correlationId = context.Request.Headers.TryGetValue(headerName, out var value)
                    && Guid.TryParse(value.ToString(), out var incoming)
                    ? incoming
                    : Guid.NewGuid();

                ExecutionContextHolder.Current = correlationId;
                context.Response.Headers[headerName] = correlationId.ToString();

                await next();
            });
        }
    }
}
