namespace Truss.Rbac
{
    /// <summary>
    /// The default scope: every assignment is global. Registered when no
    /// tenancy-aware scope has been provided.
    /// </summary>
    internal sealed class NullRoleScope : IRoleScope
    {
        public Guid? CurrentScopeId => null;
    }
}
