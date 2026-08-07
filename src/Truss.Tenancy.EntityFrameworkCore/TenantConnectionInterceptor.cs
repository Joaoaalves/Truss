using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Truss.Tenancy;

namespace Truss.Tenancy.EntityFrameworkCore
{
    /// <summary>
    /// Points every opening connection at the current tenant's database when a
    /// mapping exists. Works for any relational provider, because the switch
    /// happens on the ADO connection itself, right before it opens.
    /// </summary>
    public sealed class TenantConnectionInterceptor(ITenantConnectionStrings? connectionStrings) : DbConnectionInterceptor
    {
        /// <inheritdoc />
        public override InterceptionResult ConnectionOpening(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
        {
            Redirect(connection);
            return base.ConnectionOpening(connection, eventData, result);
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Redirect(connection);
            return base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
        }

        private void Redirect(DbConnection connection)
        {
            if (connectionStrings is null || TenantContextHolder.Current is not { } tenant)
                return;

            if (connectionStrings.ConnectionStringFor(tenant) is { } target && connection.ConnectionString != target)
                connection.ConnectionString = target;
        }
    }
}
