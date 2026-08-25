using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Truss.Rbac;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Provides methods to register role-based access control.
    /// Lives in the Microsoft.Extensions.DependencyInjection namespace so registration
    /// is available in the composition root without additional usings.
    /// </summary>
    public static class TrussRbacModule
    {
        /// <summary>
        /// Registers role-based access control: the roles defined in the options,
        /// permission policies materialized on demand for RequirePermission, and
        /// the per-request enrichment of role claims from the assignments store
        /// when one is registered.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">The role definitions and settings.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussRbac(this IServiceCollection services, Action<TrussRbacOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<TrussRbacOptions>();
            services.Configure(configure);

            services.AddAuthorization();
            services.AddMemoryCache();

            services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>());
            services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
            services.TryAddSingleton<IRoleScope, NullRoleScope>();
            services.AddScoped<IClaimsTransformation, RoleClaimsTransformation>();

            return services;
        }
    }
}

namespace Microsoft.AspNetCore.Builder
{

    /// <summary>
    /// Provides the permission convention for endpoints.
    /// </summary>
    public static class TrussRbacEndpointConventionBuilderExtensions
    {
        /// <summary>
        /// Requires the caller to hold a role granting the permission.
        /// Composes with MapCommand, MapQuery and any other endpoint builder.
        /// </summary>
        /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
        /// <param name="builder">The endpoint builder.</param>
        /// <param name="permission">The required permission.</param>
        /// <returns>The endpoint builder, for chaining.</returns>
        public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
            where TBuilder : IEndpointConventionBuilder
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(permission);

            return builder.RequireAuthorization(PermissionPolicyProvider.PolicyName(permission));
        }
    }
}
