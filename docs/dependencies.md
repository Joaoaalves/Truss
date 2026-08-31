# Dependencies

Truss ships as twenty-one packages arranged in three rings plus tooling. The rule
that decides where code lives: **a package boundary must pay for itself with an
external dependency it isolates or a deployment target it serves**. Fewer
moving parts, same discipline; the layer rules are enforced by the analyzer,
not by package walls.

- **Kernel**: no external dependencies worth mentioning; referenced by domain and application code.
- **Capabilities**: feature runtimes with only `Microsoft.Extensions.*`-class dependencies; referenced where the feature is used.
- **Integrations**: the packages that carry real third-party dependencies or a framework reference; referenced only by hosts.
- **Tooling**: the CLI, the source generators and the test host; never shipped inside the application.

This section lists the **direct dependencies added to a project** when
referencing each package.

---

## Kernel

### Truss.Domain

Domain building blocks: entities, aggregate roots, value objects, typed
identifiers, business rules and domain events.

Dependencies: none. `Truss.Domain` relies exclusively on the .NET runtime,
making it safe to reference from any domain layer or shared kernel.

### Truss.Application

The application kernel: contracts for commands, queries, handlers, pipeline
behaviors, dispatching, validation errors and unit of work, plus the
dispatcher and behavior implementations under `Truss.Application.Pipeline`,
and the auth (`Truss.Auth`), tenancy (`Truss.Tenancy`) and authorization
(`Truss.Rbac`) contracts the application layer programs against.

Dependencies:

- `Truss.Domain`
- `FluentValidation`
- `Microsoft.Extensions.DependencyInjection.Abstractions`

---

## Capabilities

### Truss.Messaging

Integration event contracts and runtime: the event marker and base record,
versioned JSON serialization (`Truss.Messaging.Serialization`), the
transactional outbox and inbox models (`Truss.Messaging.Outbox`,
`Truss.Messaging.Inbox`), the transport abstraction with the in-memory
transport (`Truss.Messaging.Transport`) and consumer dispatch
(`Truss.Messaging.Dispatch`).

Dependencies:

- `Truss.Application`
- `Microsoft.Extensions.Diagnostics.HealthChecks`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Hosting.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Options`

### Truss.Jobs

Background job contracts and runtime: the job interface, execution context,
scheduler and monitor at the root, the stores under `Truss.Jobs.Storage` and
the executor, pollers and recurring jobs under `Truss.Jobs.Runtime`.
Enqueueing is transactional through the outbox.

Dependencies:

- `Truss.Messaging`
- `Microsoft.Extensions.Diagnostics.HealthChecks`
- `Cronos`

### Truss.Email

Email contracts: the message shape, the sender and address validation
abstractions, and a console sender that prints messages during development.

Dependencies:

- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`

### Truss.Rbac

Role-based access control over standard authorization: roles defined in code,
on-demand permission policies and per-request role claims enrichment.

Dependencies:

- `Truss.Application`
- ASP.NET Core shared framework (framework reference, not a package)

### Truss.Observability

Structured request logging, spans and metrics through BCL diagnostics, and
the ambient execution context.

Dependencies:

- `Truss.Application`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`

### Truss.Support

The support surface of an application in deck mode: the wire contracts of
the Truss Deck ingestion API and a typed client that carries the service
credential, maps failures back to the local exceptions and makes retries
safe with idempotency keys.

Dependencies:

- `Truss.Application`
- `Microsoft.Extensions.Http`
- `Microsoft.Extensions.Options`

### Truss.Remote

Typed forwarding of contract queries to a context hosted as another service,
with local failure semantics.

Dependencies:

- `Truss.Application`
- `Microsoft.Extensions.Http`

---

## Integrations

### Truss.EntityFrameworkCore

Everything EF Core in one package, registered per feature: the unit of work
with domain event dispatching and idempotency records at the root, outbox and
inbox stores (`Truss.EntityFrameworkCore.Messaging`), the job store and
scheduler lock (`Truss.EntityFrameworkCore.Jobs`), tenancy interceptors
(`Truss.EntityFrameworkCore.Tenancy`) and the role assignment store
(`Truss.EntityFrameworkCore.Rbac`). Nothing activates until its module is
registered.

Dependencies:

- `Truss.Application`
- `Truss.Messaging`
- `Truss.Jobs`
- `Microsoft.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Relational`
- `Microsoft.Extensions.Diagnostics.HealthChecks`

### Truss.AspNetCore

Everything ASP.NET Core in one package: endpoint mapping for commands and
queries with automatic ProblemDetails responses, remote context endpoints,
job progress endpoints, outbox counters and dead-letter retry, correlation
middleware and tenant resolution.

Dependencies:

- `Truss.Application`
- `Truss.Messaging`
- `Truss.Jobs`
- `Truss.Observability`
- ASP.NET Core shared framework (framework reference, not a package)

### Truss.Messaging.Postgres

Durable Postgres transport: table-backed queue with SKIP LOCKED competing
consumers, LISTEN/NOTIFY wake-up, retry with backoff and a dead-letter table.

Dependencies:

- `Truss.Messaging`
- `Npgsql`

### Truss.Messaging.RabbitMq

Durable RabbitMQ transport: quorum queues with publisher confirms,
broker-side delivery accounting and dead-lettering.

Dependencies:

- `Truss.Messaging`
- `RabbitMQ.Client`

### Truss.Messaging.Redis

Durable Redis transport: Streams with consumer groups, pending message
reclaim and a dead-letter stream.

Dependencies:

- `Truss.Messaging`
- `StackExchange.Redis`

### Truss.Email.Smtp

SMTP delivery through MailKit, and the address validator combining RFC syntax
with a DNS deliverability check.

Dependencies:

- `Truss.Email`
- `MailKit`
- `DnsClient`
- `Microsoft.Extensions.Options`

### Truss.Email.Resend

Resend provider: delivery through the official API client behind the sender
abstraction.

Dependencies:

- `Truss.Email`
- `Resend`
- `Microsoft.Extensions.Http`

### Truss.Auth.Jwt

JWT authentication mechanics: PBKDF2 password hashing, access and refresh
token issuing, JwtBearer wiring.

Dependencies:

- `Truss.Application`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- ASP.NET Core shared framework (framework reference, not a package)

### Truss.Observability.OpenTelemetry

OTLP bridge: exports the Truss activity sources, meter and application logs
through the OpenTelemetry SDK, with ASP.NET Core and HttpClient
instrumentation.

Dependencies:

- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`

---

## Tooling

### Truss.Cli

The `truss` command line tool: project scaffolding, manifest-driven module
installation, code generation, service splitting, deploy artifacts and
project verification.

Dependencies: `Spectre.Console` and `Spectre.Console.Cli`. Installed as a
dotnet tool; never referenced by application code.

### Truss.Generators

Compile-time handler discovery, dispatch priming, build diagnostics and DTO
mapping (mappers generated from partial method declarations). Development
dependency: nothing is added to the published application.

Dependencies: none at runtime. Referenced with `PrivateAssets="all"` in the
composition root and application projects.

### Truss.Testing

Integration test host: boots the pipeline, a throwaway sqlite database, the
in-memory transport with the outbox and the job runtime, with helpers for
sending requests, draining the outbox deterministically and awaiting jobs.

Dependencies:

- `Truss.Application`
- `Truss.Messaging`
- `Truss.Jobs`
- `Truss.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Sqlite`
