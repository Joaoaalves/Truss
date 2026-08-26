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

## In Tests

`TrussTestHost` registers the tenancy services whenever it boots your context, so the query filter in your model has its feed and the stamp on save throws loudly without an ambient tenant, the same as production. Set the tenant inside the test method itself; an `AsyncLocal` set in the class constructor does not reach the test body.

---

## Database per Tenant

Row-level isolation in one shared database is the default and fits most applications. When a tenant needs its own database, register one mapping and nothing else changes:

```csharp
builder.Services.AddSingleton<ITenantConnectionStrings>(
    new MyTenantDirectory());   // ConnectionStringFor(tenantId) -> connection string or null
```

Every connection the context opens is pointed at the current tenant's database first, on the ADO connection itself, so it works with any relational provider. Tenants without a mapping stay on the default connection: shared and dedicated databases coexist, which is how plans usually tier. Run `truss db migrate` once per tenant database as a deploy step.

Ambient propagation through message transports remains explicit by design: integration events and job arguments carry the tenant as data when work crosses that boundary.
