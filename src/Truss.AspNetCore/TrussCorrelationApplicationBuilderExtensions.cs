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
                // The id arrives from gateways and clients in whatever shape they
                // use, so any reasonable token is accepted as-is; the cap keeps a
                // hostile header out of every log line downstream.
                var incoming = context.Request.Headers.TryGetValue(headerName, out var value)
                    ? value.ToString().Trim()
                    : string.Empty;

                var correlationId = incoming.Length is > 0 and <= 128 && incoming.All(character => !char.IsControl(character))
                    ? incoming
                    : Guid.NewGuid().ToString();

                ExecutionContextHolder.Current = correlationId;
                context.Response.Headers[headerName] = correlationId;

                await next();
            });
        }
    }
}
