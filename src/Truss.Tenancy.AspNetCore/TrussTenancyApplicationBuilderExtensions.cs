using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Truss.Tenancy;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Options for HTTP tenant resolution.
    /// </summary>
    public sealed class TrussTenancyOptions
    {
        /// <summary>
        /// Gets or sets the claim that carries the tenant of an authenticated user.
        /// Checked first. Defaults to "tenant".
        /// </summary>
        public string ClaimType { get; set; } = "tenant";

        /// <summary>
        /// Gets or sets the header that carries the tenant when no claim does.
        /// Defaults to "X-Tenant-Id".
        /// </summary>
        public string HeaderName { get; set; } = "X-Tenant-Id";

        /// <summary>
        /// Gets or sets a custom resolver that replaces the claim and header
        /// lookups entirely, for strategies like subdomains or route values.
        /// </summary>
        public Func<HttpContext, Guid?>? Resolver { get; set; }
    }

    /// <summary>
    /// Provides the middleware that resolves the tenant of each request.
    /// </summary>
    public static class TrussTenancyApplicationBuilderExtensions
    {
        /// <summary>
        /// Resolves the tenant into the ambient context: the custom resolver when
        /// one is set, otherwise the tenant claim of the authenticated user, then
        /// the header. Register it after authentication so the claim is available.
        /// Requests without a tenant flow with none, and tenant-owned data stays
        /// invisible to them.
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <param name="configure">Optional configuration of the resolution.</param>
        /// <returns>The updated <see cref="IApplicationBuilder"/>.</returns>
        public static IApplicationBuilder UseTrussTenancy(this IApplicationBuilder app, Action<TrussTenancyOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(app);

            var options = new TrussTenancyOptions();
            configure?.Invoke(options);

            return app.Use((context, next) =>
            {
                TenantContextHolder.Current = Resolve(context, options);
                return next(context);
            });
        }

        private static Guid? Resolve(HttpContext context, TrussTenancyOptions options)
        {
            if (options.Resolver is not null)
                return options.Resolver(context);

            if (context.User.FindFirst(options.ClaimType)?.Value is { } claim && Guid.TryParse(claim, out var fromClaim))
                return fromClaim;

            if (context.Request.Headers.TryGetValue(options.HeaderName, out var header)
                && !StringValues.IsNullOrEmpty(header)
                && Guid.TryParse(header.ToString(), out var fromHeader))
            {
                return fromHeader;
            }

            return null;
        }
    }
}
