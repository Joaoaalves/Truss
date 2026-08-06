using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Truss.Tenancy;

namespace Truss.Tenancy.EntityFrameworkCore
{
    /// <summary>
    /// Stamps the ambient tenant on every inserted tenant-owned entity.
    /// Inserting tenant-owned data without an ambient tenant is a bug worth
    /// failing loudly on, never a row silently visible to nobody.
    /// </summary>
    public sealed class TenantStampInterceptor : SaveChangesInterceptor
    {
        /// <inheritdoc />
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            Stamp(eventData.Context);
            return base.SavingChanges(eventData, result);
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Stamp(eventData.Context);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void Stamp(DbContext? context)
        {
            if (context is null)
                return;

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.State != EntityState.Added
                    || entry.Metadata.FindAnnotation(TrussTenancyModelBuilderExtensions.TenantOwnedAnnotation)?.Value is not true)
                {
                    continue;
                }

                if (TenantContextHolder.Current is not { } tenant)
                {
                    throw new InvalidOperationException(
                        $"Cannot save {entry.Metadata.ClrType.Name}: it is tenant-owned and no ambient tenant is set. Resolve a tenant with UseTrussTenancy, or set TenantContextHolder.Current in non-HTTP flows."
                    );
                }

                entry.Property(TrussTenancyModelBuilderExtensions.TenantIdProperty).CurrentValue = tenant;
            }
        }
    }
}
