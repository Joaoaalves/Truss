# Role-Based Access Control

`Truss.Rbac` builds on the standard ASP.NET Core authorization instead of replacing it: roles and their permissions are defined in code, endpoints require permissions with one chained call, and only the user-role assignments live in the database.

```
truss add rbac
```

---

## Roles in Code, Assignments in Data

Roles are part of the application's design, so they live where design lives, reviewed and versioned like any other change:

```csharp
builder.Services.AddTrussRbac(options =>
{
    options.AddRole("admin", "catalog.write", "orders.refund", "reports.read");
    options.AddRole("support", "orders.read", "reports.read");
});

builder.Services.AddTrussRbacEntityFramework<AppDbContext>();
```

Which user holds which role is data, granted and revoked through `IRoleAssignments`:

```csharp
await roles.Assign(userId, "admin");
```

A [seeder](cli.md) granting the first admin in development is the natural starting point. Role definitions never touch the domain model, and the module works with or without every other Truss module, tenancy included.

---

## Protecting Endpoints

```csharp
app.MapCommand<RefundOrder>("/orders/refund").RequirePermission("orders.refund");
```

`RequirePermission` composes with `MapCommand`, `MapQuery` and any endpoint builder. Policies materialize on demand, so there is nothing to pre-register per permission. A caller without a granting role receives 403; an anonymous caller, 401.

Handlers that need to check permissions imperatively read the user's claims as usual; the permission model stays in one place, the options.

---

## How Roles Reach the Request

A claims transformation resolves the user's stored roles on each request from the `sub` claim, with a short cache (30 seconds by default). The consequences are deliberate:

- Login handlers stay untouched and tokens stay lean; granting or revoking a role needs no re-login and applies within the cache window.
- Any authentication that produces a `sub` or name identifier claim works: the scaffolded JWT auth, Identity, or an external provider.
- Role claims already present in the token are honored too, so token-embedded roles work without the store.
- The enriched identity names its role claim type, so `User.IsInRole` and `[Authorize(Roles = ...)]` see stored roles exactly like `RequirePermission` does.

Configure the claim type and cache with `TrussRbacOptions`.

---

## After a Split

`truss split` copies the role map and the assignment store into a service whose moved routes carry `RequirePermission`. Grants do not travel: each service owns its database, so the same user needs their roles granted in each database that enforces them. A valid token answering 403 in a freshly split service almost always means the grant exists only in the monolith's database.

---

## Tenant-Scoped Grants

In multi-tenant applications, a grant can apply inside one tenant only:

```csharp
await roles.Assign(userId, "admin");             // global: every tenant
await roles.Assign(userId, "admin", tenantId);   // only inside that tenant
```

With tenancy installed, resolution binds to the ambient tenant automatically: a user sees their global roles everywhere and their scoped roles only inside the granting tenant. Without tenancy, every assignment behaves globally and nothing changes; the modules stay independent.
