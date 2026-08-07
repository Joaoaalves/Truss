namespace Truss.Rbac
{
    /// <summary>
    /// The scope of role assignments for the current request. Without one,
    /// every assignment is global; with tenancy installed, the EF store binds
    /// it to the ambient tenant, so grants can be tenant-scoped.
    /// </summary>
    public interface IRoleScope
    {
        /// <summary>
        /// Gets the current scope identifier, or null for the global scope.
        /// </summary>
        Guid? CurrentScopeId { get; }
    }

    internal sealed class NullRoleScope : IRoleScope
    {
        public Guid? CurrentScopeId => null;
    }
}
