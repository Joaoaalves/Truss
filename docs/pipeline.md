# Pipeline Behaviors

Every request dispatched through Truss flows through a pipeline of behaviors that wrap the handler, middleware-style.

---

## The Contract

```csharp
public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
```

A behavior can run logic before and after `next()`, short-circuit the pipeline by not calling it, or transform failures. There is exactly one pipeline system. Commands and queries share it.

---

## Ordering

Behaviors execute in registration order: the first registered behavior is the outermost.

```
ValidationBehavior            registered by AddTruss
    UnitOfWorkBehavior        registered by AddTrussEntityFramework
        handler
```

This order is intentional: validation failures must prevent the unit of work from ever being touched.

---

## Validation

`ValidationBehavior` runs every FluentValidation validator registered for the request type. Validators are discovered automatically from the assemblies given to `AddTruss`.

```csharp
public class CreateUserValidator : AbstractValidator<CreateUser>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Email).NotEmpty().EmailAddress();
    }
}
```

When validation fails:

- The handler is not executed.
- The unit of work is not created.
- A `RequestValidationException` is thrown containing every failure, not just the first one.

```csharp
public class RequestValidationException : Exception
{
    public IReadOnlyList<ValidationError> Errors { get; }
}
```

Each `ValidationError` carries the property name and the message, ready to be mapped to an API error response.

> The upcoming ASP.NET Core module will map `RequestValidationException` to an RFC 7807 `ProblemDetails` response automatically. See the [Roadmap](roadmap.md).

---

## Unit of Work Behavior

`UnitOfWorkBehavior` applies to commands only. Its type constraint (`where TRequest : ICommand<TResponse>`) makes the container skip it for queries entirely.

On success, it commits the unit of work. On failure, nothing is committed and the exception propagates unchanged. Details in [Unit of Work](unit-of-work.md).

---

## Custom Behaviors

Register your own behaviors for cross-cutting concerns such as logging, caching or metrics:

```csharp
public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {Request}", typeof(TRequest).Name);
        var response = await next();
        logger.LogInformation("Handled {Request}", typeof(TRequest).Name);
        return response;
    }
}
```

```csharp
services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
```

Open generic registrations with type constraints are supported. The container skips a behavior whenever the request type does not satisfy its constraints, which is exactly how `UnitOfWorkBehavior` targets commands only.

---

## What AddTruss registers for you

Besides the dispatcher, the validation behavior and your handlers, `AddTruss` registers `TimeProvider.System` when the host has not registered one. Handlers that reason about time take `TimeProvider` and stay testable, and the [test host](testing.md) can replace it with a fake before the pipeline is built.
