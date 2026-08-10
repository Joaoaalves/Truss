# ASP.NET Core

`Truss.AspNetCore` turns commands and queries into minimal API endpoints. The command is the request body, validation runs before the handler, and failures come back as standard ProblemDetails responses. No controller, no manual binding, no try/catch.

Routes are always explicit. Truss never infers a route from a type name.

---

## Mapping Commands

Commands map to POST endpoints. The request body binds to the command record:

```csharp
app.MapCommand<CreateUser, Guid>("/users");
```

| Overload | Success response |
|---|---|
| `MapCommand<TCommand>(pattern)` | 204 No Content |
| `MapCommand<TCommand, TResult>(pattern)` | 200 OK with the result |
| `MapCommand<TCommand, TResult>(pattern, location)` | 201 Created with the result and a Location header |

The 201 overload receives a function that builds the Location header from the result:

```csharp
app.MapCommand<CreateUser, Guid>("/users", id => $"/users/{id}");
```

---

## Queries that may find nothing

A query declared as `IQuery<T?>` answers "not found" with null, and `MapQuery` turns that into a 404. Returning 200 with an empty body would make every client check twice.

---

## Endpoints you map by hand

`MapCommand` maps POST and `MapQuery` maps GET. Any other verb is a plain minimal API endpoint, and one call gives it the same error translation:

```csharp
app.MapDelete("/diary/{date}/items/{itemId}", async (DateOnly date, Guid itemId, IDispatcher dispatcher, CancellationToken ct) =>
{
    await dispatcher.Send(new RemoveDiaryItem(date, itemId), ct);
    return Results.NoContent();
})
.AddTrussErrorHandling();
```

Without it, a broken invariant leaves the endpoint as a 500 instead of the 422 the rest of the API returns.

---

## Mapping Queries

Queries map to GET endpoints. Route values and the query string bind to the query record:

```csharp
public sealed record GetUserById(Guid Id) : IQuery<UserDto?>;

app.MapQuery<GetUserById, UserDto?>("/users/{id}");
```

A request to `/users/7d9f...` binds `Id` from the route. Parameters not present in the route bind from the query string:

```csharp
public sealed record SearchUsers(string Term, int Page) : IQuery<List<UserDto>>;

app.MapQuery<SearchUsers, List<UserDto>>("/users");
// GET /users?term=joao&page=2
```

---

## Error Responses

Both mappings attach a filter that converts Truss exceptions into RFC 7807 responses.

### Validation failures

`RequestValidationException` becomes a 400 with one entry per property, carrying every message:

```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Name": ["'Name' must not be empty."],
    "Email": ["'Email' is not a valid email address."]
  }
}
```

### Business rule violations

`BusinessRuleValidationException` becomes a 422 carrying the rule that was broken:

```json
{
  "title": "A business rule was violated.",
  "status": 422,
  "detail": "An order must contain at least one item.",
  "rule": "OrderMustHaveItemsRule",
  "code": "OrderMustHaveItemsRule"
}
```

`code` is the stable contract for clients that branch on specific errors. It defaults to the rule's type name; override `Code` on the rule to pin a wire value (for example `"orders.no-items"`) that survives renames. `rule` stays the raw type name for diagnostics.

Any other exception propagates unchanged and is handled by your regular exception middleware.

---

## Composing with the Endpoint Pipeline

Every mapping returns a `RouteHandlerBuilder`, so the regular minimal API surface keeps working:

```csharp
app.MapCommand<DeactivateUser>("/users/deactivate")
    .RequireAuthorization("admin")
    .WithTags("Users");
```

The mappings also register OpenAPI metadata (success status, 400 and 422) so generated API documentation is accurate out of the box.

---

## Registration

There is nothing to register. Reference `Truss.AspNetCore` from the host project and map endpoints. The package depends only on `Truss.Application.Abstractions`; the runtime comes from your existing `AddTruss` call.
