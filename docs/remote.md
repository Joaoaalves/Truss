# Remote Contexts

When a bounded context becomes [its own service](cli.md#truss-split), most communication should stay what it already was: integration events through the [outbox](messaging.md). Events keep services decoupled in time; a synchronous call couples your latency and your availability to someone else's. That said, a synchronous **query** between services is legitimate: showing a screen sometimes needs an answer another service owns, now.

`Truss.Remote` makes that call possible without making it invisible.

---

## The contract

What a service exposes lives in its contracts project, created by `truss split`: `src/MyShop.Sales.Contracts`. A query another service may ask goes there with its result DTO:

```csharp
namespace MyShop.Sales.Contracts;

public sealed record OrderSummaryDto(Guid Id, string Name, decimal Total);

public sealed record GetOrderSummary(Guid OrderId) : IQuery<OrderSummaryDto?>;
```

The Sales service keeps its handler where it always was; only the record and the DTO move to the contract. **Commands stay out by design**: a synchronous command between services is coupling in disguise; publish an integration event instead.

---

## Serving

The split scaffolds this into the service's Program:

```csharp
app.MapRemoteContext(typeof(SalesContracts).Assembly);
```

Every `IQuery` of the contracts assembly answers at `POST /truss/remote/{query full name}`, dispatched through the service's regular pipeline: validation, business rules, unit of work. Nothing outside the declared contract is reachable. The returned group composes like any endpoint group; protect it with `.RequireAuthorization()` when the constellation is not on a private network.

---

## Calling

The consuming service references the contracts project and declares, in its composition root, that Sales lives somewhere else:

```csharp
builder.Services.AddRemoteContext<SalesContracts>("Sales", new Uri("http://localhost:5101"), options =>
{
    options.Timeout = TimeSpan.FromSeconds(3);
});
```

Callers keep dispatching as always; the handler does not know or care that the answer crossed the wire:

```csharp
var summary = await dispatcher.Send(new GetOrderSummary(orderId));
```

This is deliberate and bounded: the network is visible exactly once, in the composition root, with the timeout beside it. There is no service discovery, no proxy magic, no interface generation. The registered `HttpClient` is named `truss-remote-Sales`, so resilience policies attach the standard way:

```csharp
builder.Services.AddHttpClient("truss-remote-Sales").AddStandardResilienceHandler();
```

---

## Failure semantics

A remote query answers exactly like a local one:

| Remote outcome | On the caller |
|---|---|
| Result | The result |
| Null (query declared `IQuery<T?>`) | Null |
| Validation failure (400) | `RequestValidationException` with the same errors |
| Business rule violation (422) | `BusinessRuleValidationException` carrying the rule's stable `Code` |
| Unreachable, timeout, unknown query | `RemoteContextException` |

The first four mean a caller cannot tell whether the context is in-process or across the network, which is what makes extraction mechanical. The last one is the honest difference: the network is allowed to fail, and the exception names the context and the address so the failure reads like what it is.
