using Microsoft.EntityFrameworkCore;
using Truss.Rbac;

namespace Truss.Rbac.EntityFrameworkCore
{
    /// <summary>
    /// One row per user and role.
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
        public UserRoleRecord(Guid userId, string role)
        {
            UserId = userId;
            Role = role;
        }

        /// <summary>Gets the user identifier.</summary>
        public Guid UserId { get; private set; }

        /// <summary>Gets the role name.</summary>
        public string Role { get; private set; }
    }

    /// <summary>
    /// EF Core role assignments. Grant and revoke persist immediately;
    /// enforcement picks changes up within the role cache duration.
    /// </summary>
    /// <typeparam name="TDbContext">The context that owns the assignments table.</typeparam>
    public class EfRoleAssignments<TDbContext>(TDbContext context) : IRoleAssignments
        where TDbContext : DbContext
    {
        private readonly TDbContext _context = context;

        /// <inheritdoc />
        public async Task<IReadOnlyList<string>> RolesOf(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.Set<UserRoleRecord>()
                .Where(record => record.UserId == userId)
                .Select(record => record.Role)
                .ToListAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task Assign(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            var exists = await _context.Set<UserRoleRecord>()
                .AnyAsync(record => record.UserId == userId && record.Role == role, cancellationToken);

            if (exists)
                return;

            _context.Set<UserRoleRecord>().Add(new UserRoleRecord(userId, role));
            await _context.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task Revoke(Guid userId, string role, CancellationToken cancellationToken = default)
        {
            await _context.Set<UserRoleRecord>()
                .Where(record => record.UserId == userId && record.Role == role)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}

namespace Microsoft.EntityFrameworkCore
{
    using Truss.Rbac.EntityFrameworkCore;

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
                builder.HasKey(record => new { record.UserId, record.Role });
                builder.Property(record => record.Role).HasMaxLength(128);
            });

            return modelBuilder;
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using Microsoft.EntityFrameworkCore;
    using Truss.Rbac;
    using Truss.Rbac.EntityFrameworkCore;

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

            return services;
        }
    }
}
