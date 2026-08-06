using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Truss.Rbac
{
    /// <summary>
    /// The requirement behind RequirePermission: the user must hold a role
    /// that grants the permission.
    /// </summary>
    /// <param name="Permission">The required permission.</param>
    public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

    /// <summary>
    /// Grants a permission requirement when any of the user's roles maps to it
    /// in the configured role definitions.
    /// </summary>
    public sealed class PermissionHandler(IOptions<TrussRbacOptions> options) : AuthorizationHandler<PermissionRequirement>
    {
        private readonly TrussRbacOptions _options = options.Value;

        /// <inheritdoc />
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var granted = context.User.Claims
                .Where(claim => claim.Type == _options.RoleClaimType || claim.Type == ClaimTypes.Role)
                .Any(claim => _options.PermissionsOf(claim.Value).Contains(requirement.Permission));

            if (granted)
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Materializes permission policies on demand, so RequirePermission needs no
    /// per-permission registration; everything else falls back to the default provider.
    /// </summary>
    public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
    {
        private const string Prefix = "truss:permission:";

        private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

        /// <summary>
        /// Builds the policy name of a permission.
        /// </summary>
        public static string PolicyName(string permission) => Prefix + permission;

        /// <inheritdoc />
        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
                return _fallback.GetPolicyAsync(policyName);

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        /// <inheritdoc />
        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

        /// <inheritdoc />
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
    }
}
