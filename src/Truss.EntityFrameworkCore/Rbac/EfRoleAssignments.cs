using Microsoft.EntityFrameworkCore;
using Truss.Rbac;

namespace Truss.EntityFrameworkCore.Rbac
{
    /// <summary>
    /// One row per user, role and scope. A null tenant means a global grant.
    /// </summary>
    public class UserRoleRecord
    {
        private UserRoleRecord()
        {
            Role = string.Empty;
        }

        /// <summary>
        /// Creates an assignment.
        /// </summary>
        public UserRoleRecord(Guid id, Guid userId, string role, Guid? tenantId)
        {
            Id = id;
            UserId = userId;
            Role = role;
            TenantId = tenantId;
        }

        /// <summary>Gets the record identifier.</summary>
        public Guid Id { get; private set; }

        /// <summary>Gets the user identifier.</summary>
        public Guid UserId { get; private set; }

        /// <summary>Gets the role name.</summary>
        public string Role { get; private set; }

        /// <summary>Gets the tenant of the grant, or null for a global one.</summary>
        public Guid? TenantId { get; private set; }
    }

    /// <summary>
    /// EF Core role assignments. Grant and revoke persist immediately;
    /// enforcement picks changes up within the role cache duration.
    /// </summary>
    /// <typeparam name="TDbContext">The context that owns the assignments table.</typeparam>
    public class EfRoleAssignments<TDbContext>(TDbContext context, IRoleScope scope) : IRoleAssignments
        where TDbContext : DbContext
    {
        private readonly TDbContext _context = context;

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> RolesOf(Guid userId, CancellationToken cancellationToken = default)
        {
            var scopeId = scope.CurrentScopeId;

            return await _context.Set<UserRoleRecord>()
                .Where(record => record.UserId == userId && (record.TenantId == null || record.TenantId == scopeId))
                .Select(record => record.Role)
                .Distinct()
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task Assign(Guid userId, string role, Guid? scopeId = null, CancellationToken cancellationToken = default)
        {
            var exists = await _context.Set<UserRoleRecord>()
                .AnyAsync(record => record.UserId == userId && record.Role == role && record.TenantId == scopeId, cancellationToken);

            if (exists)
                return;

            _context.Set<UserRoleRecord>().Add(new UserRoleRecord(Guid.NewGuid(), userId, role, scopeId));
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task Revoke(Guid userId, string role, Guid? scopeId = null, CancellationToken cancellationToken = default)
        {
            await _context.Set<UserRoleRecord>()
                .Where(record => record.UserId == userId && record.Role == role && record.TenantId == scopeId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Binds the role scope to the ambient tenant, so grants can be tenant-scoped
    /// when tenancy is in use. Without tenancy, the ambient tenant is always null
    /// and every assignment behaves globally.
    /// </summary>
    public sealed class TenantRoleScope : IRoleScope
    {
        /// <inheritdoc />
        public Guid? CurrentScopeId => Truss.Tenancy.TenantContextHolder.Current;
    }
}

namespace Microsoft.EntityFrameworkCore
{
    using Truss.EntityFrameworkCore;
    using Truss.EntityFrameworkCore.Rbac;

    /// <summary>
    /// Provides the model configuration for role assignments.
    /// </summary>
    public static class TrussRbacModelBuilderExtensions
    {
        /// <summary>
        /// Adds the role assignments table to the model.
        /// Call this from OnModelCreating in the context passed to AddTrussRbacEntityFramework.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        /// <returns>The updated <see cref="ModelBuilder"/>.</returns>
        public static ModelBuilder ApplyTrussRbac(this ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);

            modelBuilder.Entity<UserRoleRecord>(builder =>
            {
                builder.ToTable("TrussUserRoles");
                builder.HasKey(record => record.Id);
                builder.Property(record => record.Role).HasMaxLength(128);
                builder.HasIndex(record => new { record.UserId, record.Role, record.TenantId }).IsUnique();
            });

            return modelBuilder;
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Truss.EntityFrameworkCore;
    using Truss.EntityFrameworkCore.Rbac;

    /// <summary>
    /// Provides methods to register the EF Core role assignments.
    /// </summary>
    public static class TrussRbacEntityFrameworkModule
    {
        /// <summary>
        /// Registers the role assignments store for the given context.
        /// Call after AddTrussRbac, and add the table to the context model
        /// with modelBuilder.ApplyTrussRbac().
        /// </summary>
        /// <typeparam name="TDbContext">The context that owns the assignments table.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <returns>The updated <see cref="IServiceCollection"/>.</returns>
        public static IServiceCollection AddTrussRbacEntityFramework<TDbContext>(this IServiceCollection services)
            where TDbContext : DbContext
        {
            services.AddScoped<IRoleAssignments, EfRoleAssignments<TDbContext>>();
            services.Replace(ServiceDescriptor.Singleton<IRoleScope, TenantRoleScope>());

            return services;
        }
    }
}
