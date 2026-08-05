# Roadmap

Truss is built module by module, each one opt-in. The framework never installs anything you did not ask for.

---

## Delivered

- Domain building blocks: entities with identity equality, aggregate roots, value objects, typed ids, business rules, domain events.
- Single pipeline with explicit ordering, validation that reports every failure, cancellation propagated end to end.
- Dispatcher with typed invokers cached per request type. No `dynamic`, no per-call reflection.
- EF Core unit of work: automatic commit, cascading domain event dispatch, single atomic save.
- ASP.NET Core module: `MapCommand` and `MapQuery` minimal API extensions, validation failures as RFC 7807 `ProblemDetails` with every field error, business rule violations as 422 responses.

---

## Next

### Source generators

Compile-time dispatch and registration: no startup assembly scanning, missing handlers become build errors, full Native AOT support.

### Messaging and integration events

- Outbox pattern: integration events stored with the commit, published after it.
- Broker adapters, free options first (RabbitMQ, Redis).

---

## Planned

| Module | Purpose |
|---|---|
| CLI (`truss`) | Project scaffolding, `truss new`, `truss add`, manifest-driven module installation |
| Auth | Pluggable authentication modules with a user model scaffolded into your domain, fully editable |
| Jobs and queues | Background jobs with progress tracking (streaming, websockets or polling), pluggable brokers, dashboard |
| Observability | Structured logging, OpenTelemetry, optional dashboards, enabled only if you want them |
| Mapping | Source-generated DTO mapping |

---

## Principles that will not change

- Every module is opt-in.
- Free and open-source options first.
- The domain layer stays dependency-free.
- No runtime magic.
