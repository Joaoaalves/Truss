using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Truss.Rbac
{
    /// <summary>
    /// Adds the user's stored roles as claims on every request, resolved through
    /// the assignments store with a short cache. Tokens stay lean, login handlers
    /// stay untouched, and role changes apply within the cache window.
    /// Without a registered store, roles come only from the token.
    /// </summary>
    public sealed class RoleClaimsTransformation(
        IEnumerable<IRoleAssignments> assignments,
        IRoleScope scope,
        IMemoryCache cache,
        IOptions<TrussRbacOptions> options) : IClaimsTransformation
    {
        private readonly IRoleAssignments? _assignments = assignments.FirstOrDefault();
        private readonly TrussRbacOptions _options = options.Value;

        /// <inheritdoc />
        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (_assignments is null || principal.Identity?.IsAuthenticated != true)
                return principal;

            var subject = principal.FindFirst("sub")?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(subject, out var userId))
                return principal;

            var roles = await cache.GetOrCreateAsync($"truss:rbac:roles:{userId}:{scope.CurrentScopeId}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = _options.RoleCacheDuration;
                return await _assignments.RolesOf(userId);
            }) ?? [];

            var held = principal.Claims
                .Where(claim => claim.Type == _options.RoleClaimType || claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missing = roles.Where(role => !held.Contains(role)).ToList();

            if (missing.Count == 0)
                return principal;

            // Transformations can run more than once per request; cloning keeps
            // the incoming principal untouched and the operation repeatable.
            // The added identity must name its own role claim type: IsInRole
            // asks every identity through the identity's RoleClaimType, and a
            // ClaimsIdentity born without one reads the SOAP-era default and
            // never finds the roles it carries.
            var enriched = principal.Clone();
            enriched.AddIdentity(new ClaimsIdentity(
                missing.Select(role => new Claim(_options.RoleClaimType, role)),
                authenticationType: null,
                nameType: null,
                roleType: _options.RoleClaimType));

            return enriched;
        }
    }
}
