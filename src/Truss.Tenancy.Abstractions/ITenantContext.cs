namespace Truss.Tenancy
{
    /// <summary>
    /// Read access to the current tenant for handlers and services that need it.
    /// </summary>
    public interface ITenantContext
    {
        /// <summary>
        /// Gets the tenant of the current request, or null outside one.
        /// </summary>
        Guid? TenantId { get; }
    }

    /// <summary>
    /// The default tenant context, reading the ambient holder.
    /// </summary>
    public sealed class AmbientTenantContext : ITenantContext
    {
        /// <inheritdoc />
        public Guid? TenantId => TenantContextHolder.Current;
    }
}
