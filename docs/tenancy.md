# Multi-Tenancy

`Truss.Tenancy` isolates tenant data at the row level with a clean domain: no entity carries a `TenantId`, no handler filters by hand, and forgetting a `Where` clause cannot leak another tenant's rows.

```
truss add tenancy
```

Three pieces land: the ambient tenant context, the HTTP resolution and the EF Core isolation, all opt-in and independent of every other module.

---

## Marking What Belongs to a Tenant

The domain type stays untouched. The marking happens where persistence decisions already live, in the entity's configuration:

```csharp
public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.IsTenantOwned();
    }
}
```

`ApplyTrussTenancy(this)`, which the CLI adds to `OnModelCreating`, gives every marked entity a shadow `TenantId` column with an index, a global query filter bound to the tenant of the current request, and an automatic stamp on insert. Unmarked entities are shared and completely unaffected.

The semantics are strict on purpose:

- Reads only ever see the current tenant's rows; a request without a tenant sees none.
- Inserting tenant-owned data without an ambient tenant throws, loudly, instead of writing a row visible to nobody.
- `IgnoreQueryFilters()` remains the explicit, visible escape hatch for administrative queries.

---

## Resolving the Tenant

`app.UseTrussTenancy()` resolves the tenant into the ambient context on every request: the `tenant` claim of the authenticated user first, then the `X-Tenant-Id` header. Both are configurable, and a custom resolver replaces them entirely for strategies like subdomains:

```csharp
app.UseTrussTenancy(options =>
    options.Resolver = context => ResolveFromSubdomain(context.Request.Host));
```

Register it after authentication so the claim is available. Handlers and services that need the tenant inject `ITenantContext`:

```csharp
public class ListProjectsHandler(AppDbContext context, ITenantContext tenant) : ...
```

Outside HTTP (workers, tests), set `TenantContextHolder.Current` explicitly; integration events and job arguments should carry the tenant as data when the work crosses that boundary.

---

## What It Is Not

This is shared-database isolation, the shape that fits most applications and every free tier. Database-per-tenant, tenant-scoped roles and ambient propagation through transports are deliberate future work; the seams (ambient context, marked entities) are where they will plug in.
