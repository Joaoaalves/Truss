# Quickstart

The fastest start is the [truss CLI](cli.md): one command scaffolds a complete Clean Architecture solution with a working sample.

```
dotnet tool install -g Truss.Cli
truss new MyShop --database postgres --docker
cd MyShop
truss dev
```

`truss dev` starts the docker dependencies, prints the URLs that matter and runs the API with hot reload; the scaffold serves an interactive API reference at `/scalar` in development. The rest of this page describes the manual configuration, which is also what the scaffolded code contains. All packages are on [nuget.org](https://www.nuget.org/packages?q=Truss.).

---

## Package Selection by Layer

A typical Clean Architecture setup:

| Layer | References |
|---|---|
| Domain | `Truss.Domain` |
| Application | `Truss.Application.Abstractions` |
| Infrastructure | `Truss.Persistence.EntityFrameworkCore` |
| API / Composition root | `Truss.Application`, `Truss.AspNetCore` and infrastructure modules |

---

## Composition Root

Registration extensions live in the `Microsoft.Extensions.DependencyInjection` namespace, so no extra usings are required:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddTruss(options =>
{
    options.AddAssembly<CreateUser>();
});

builder.Services.AddTrussEntityFramework<AppDbContext>();
```

`AddTruss` requires at least one assembly. It scans the registered assemblies for command, query and domain event handlers, and for FluentValidation validators. There is no fallback to all loaded assemblies. Discovery is always explicit.

---

## Your First Command

A command, its validator and its handler:

```csharp
public sealed record CreateUser(string Name, string Email) : ICommand<Guid>;

public class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}

public class CreateUserHandler(AppDbContext context) : ICommandHandler<CreateUser, Guid>
{
    public Task<Guid> Handle(CreateUser command, CancellationToken cancellationToken)
    {
        var user = User.Create(command.Name, command.Email);
        context.Users.Add(user);
        return Task.FromResult(user.Id.Value);
    }
}
```

Note what the handler does not do: it does not validate the command, does not call `SaveChangesAsync` and does not dispatch events. The pipeline does all of that.

---

## Exposing the Command as an Endpoint

With `Truss.AspNetCore`, one line turns the command into a route. The request body binds to the command and validation errors come back as ProblemDetails:

```csharp
app.MapCommand<CreateUser, Guid>("/users");
```

Prefer manual control? Inject `IDispatcher` anywhere and send the command yourself:

```csharp
app.MapPost("/users", (CreateUser command, IDispatcher dispatcher, CancellationToken ct)
    => dispatcher.Send(command, ct));
```

The execution flow for this request:

1. Validation runs. If it fails, a `RequestValidationException` carrying every failure is thrown and the handler never executes.
2. The handler runs.
3. Domain events raised by aggregates are dispatched.
4. All changes are persisted in a single atomic save.
5. The result is returned.

---

## Next Steps

- [Building Blocks](domain.md) covers modeling your domain.
- [Commands & Queries](commands-and-queries.md) covers the messaging model.
- [ASP.NET Core](aspnetcore.md) covers endpoint mapping and error responses.
- [Unit of Work](unit-of-work.md) covers the transactional boundary in depth.
