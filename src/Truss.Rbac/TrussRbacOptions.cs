namespace Truss.Rbac
{
    /// <summary>
    /// Options for role-based access control. Roles are defined here, in code:
    /// explicit, versioned with the application and reviewed like any other change.
    /// The database only stores which user holds which role.
    /// </summary>
    public sealed class TrussRbacOptions
    {
        internal Dictionary<string, HashSet<string>> Roles { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets or sets the claim type that carries roles. Defaults to "role",
        /// the raw JWT convention; the standard role claim type is always
        /// honored as well.
        /// </summary>
        public string RoleClaimType { get; set; } = "role";

        /// <summary>
        /// Gets or sets how long resolved role assignments are cached per user.
        /// Defaults to 30 seconds: role changes apply within that window without
        /// a database hit per request.
        /// </summary>
        public TimeSpan RoleCacheDuration { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Defines a role and the permissions it grants.
        /// </summary>
        /// <param name="name">The role name.</param>
        /// <param name="permissions">The permissions the role grants.</param>
        /// <returns>The options, for chaining.</returns>
        public TrussRbacOptions AddRole(string name, params string[] permissions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            if (!Roles.TryGetValue(name, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Roles[name] = set;
            }

            foreach (var permission in permissions)
                set.Add(permission);

            return this;
        }

        internal IReadOnlySet<string> PermissionsOf(string role)
        {
            return Roles.TryGetValue(role, out var set) ? set : EmptyPermissions;
        }

        private static readonly HashSet<string> EmptyPermissions = [];
    }
}
