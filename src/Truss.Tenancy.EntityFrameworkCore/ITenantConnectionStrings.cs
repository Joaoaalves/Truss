namespace Truss.Tenancy.EntityFrameworkCore
{
    /// <summary>
    /// Maps tenants to their own database. Registering an implementation turns
    /// on database-per-tenant: every connection the context opens is pointed at
    /// the current tenant's database first. Tenants without a mapping stay on
    /// the default connection, so shared and dedicated databases can coexist.
    /// </summary>
    public interface ITenantConnectionStrings
    {
        /// <summary>
        /// Returns the connection string of a tenant, or null to use the default.
        /// </summary>
        /// <param name="tenantId">The tenant identifier.</param>
        string? ConnectionStringFor(Guid tenantId);
    }
}
