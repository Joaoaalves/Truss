using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Truss.Application;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Provides the middleware that feeds the idempotency pipeline behavior.
    /// </summary>
    public static class TrussIdempotencyApplicationBuilderExtensions
    {
        /// <summary>
        /// Reads the Idempotency-Key request header into the ambient holder, where
        /// the pipeline behavior registered by AddTrussIdempotency picks it up.
        /// Requests without the header flow through untouched.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>The updated <see cref="IApplicationBuilder"/>.</returns>
        public static IApplicationBuilder UseTrussIdempotency(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            return app.Use((context, next) =>
            {
                if (context.Request.Headers.TryGetValue("Idempotency-Key", out var value)
                    && !StringValues.IsNullOrEmpty(value))
                {
                    IdempotencyKeyHolder.Current = value.ToString();
                }

                return next(context);
            });
        }
    }
}
