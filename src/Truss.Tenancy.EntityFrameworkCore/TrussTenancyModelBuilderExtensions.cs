using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Truss.Tenancy;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    /// Provides the model configuration for tenant isolation.
    /// Lives in the Microsoft.EntityFrameworkCore namespace so it is available
    /// inside OnModelCreating and entity configurations without additional usings.
    /// </summary>
    public static class TrussTenancyModelBuilderExtensions
    {
        internal const string TenantOwnedAnnotation = "Truss:TenantOwned";

        internal const string TenantIdProperty = "TenantId";

        /// <summary>
        /// Marks the entity as owned by a tenant. The domain type stays untouched:
        /// the tenant id lives in a shadow column, reads are filtered to the current
        /// tenant and inserts are stamped automatically.
        /// </summary>
        /// <typeparam name="TEntity">The entity type.</typeparam>
        /// <param name="builder">The entity type builder.</param>
        /// <returns>The updated <see cref="EntityTypeBuilder{TEntity}"/>.</returns>
        public static EntityTypeBuilder<TEntity> IsTenantOwned<TEntity>(this EntityTypeBuilder<TEntity> builder)
            where TEntity : class
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.HasAnnotation(TenantOwnedAnnotation, true);
            return builder;
        }

        /// <summary>
        /// Applies tenant isolation to every entity marked with IsTenantOwned:
        /// the shadow TenantId column with an index, and a global query filter bound
        /// to the ambient tenant of each request. Call it at the end of OnModelCreating,
        /// after the configurations that mark the entities, passing the context itself.
        /// Without an ambient tenant, tenant-owned data is invisible; IgnoreQueryFilters
        /// remains the explicit escape hatch.
        /// </summary>
        /// <param name="modelBuilder">The model builder.</param>
        /// <param name="context">The context being configured.</param>
        /// <returns>The updated <see cref="ModelBuilder"/>.</returns>
        public static ModelBuilder ApplyTrussTenancy(this ModelBuilder modelBuilder, DbContext context)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            ArgumentNullException.ThrowIfNull(context);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (entityType.FindAnnotation(TenantOwnedAnnotation)?.Value is not true)
                    continue;

                var entity = modelBuilder.Entity(entityType.ClrType);
                entity.Property<Guid>(TenantIdProperty);
                entity.HasIndex(TenantIdProperty);
                entity.HasQueryFilter(BuildFilter(entityType.ClrType, context));
            }

            return modelBuilder;
        }

        /// <summary>
        /// Reads the ambient tenant for the query filter. The context parameter is
        /// what makes the framework re-evaluate the value on every query instead of
        /// baking it into the cached model.
        /// </summary>
        public static Guid? CurrentTenant(DbContext context) => TenantContextHolder.Current;

        private static LambdaExpression BuildFilter(Type entityType, DbContext context)
        {
            var parameter = Expression.Parameter(entityType, "entity");

            var tenantColumn = Expression.Call(
                typeof(EF), nameof(EF.Property), [typeof(Guid?)],
                parameter, Expression.Constant(TenantIdProperty));

            var currentTenant = Expression.Call(
                typeof(TrussTenancyModelBuilderExtensions), nameof(CurrentTenant), null,
                Expression.Constant(context, typeof(DbContext)));

            return Expression.Lambda(Expression.Equal(tenantColumn, currentTenant), parameter);
        }
    }
}
