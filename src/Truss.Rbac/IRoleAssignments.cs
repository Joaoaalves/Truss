namespace Truss.Rbac
{
    /// <summary>
    /// Persistence contract for which user holds which role.
    /// Role definitions live in code; only the assignments are data.
    /// </summary>
    public interface IRoleAssignments
    {
        /// <summary>
        /// Returns the roles of a user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task<IReadOnlyList<string>> RolesOf(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Grants a role to a user. Granting an already held role is a no-op.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="role">The role name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Assign(Guid userId, string role, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes a role from a user. Takes effect within the role cache duration.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="role">The role name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        Task Revoke(Guid userId, string role, CancellationToken cancellationToken = default);
    }
}
