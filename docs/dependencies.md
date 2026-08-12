# Dependencies

This section lists the **direct dependencies added to a project** when referencing each Truss package.

---

## Truss.Domain

**Purpose:**
Domain building blocks: entities, aggregate roots, value objects, typed identifiers, business rules and domain events.

### Dependencies

None. `Truss.Domain` relies exclusively on the .NET runtime, making it safe to reference from any domain layer or shared kernel.

---

## Truss.Application.Abstractions

**Purpose:**
Contracts for commands, queries, handlers, pipeline behaviors, dispatching, validation errors and unit of work.

### Dependencies

- `Truss.Domain`

---

## Truss.Application

**Purpose:**
The execution pipeline: dispatcher, domain event dispatcher, validation behavior, unit of work behavior and handler registration.

### Dependencies

- `Truss.Application.Abstractions`
- `FluentValidation`
- `Microsoft.Extensions.DependencyInjection.Abstractions`

---

## Truss.Persistence.EntityFrameworkCore

**Purpose:**
EF Core implementation of the unit of work, with domain event collection from tracked entities.

### Dependencies

- `Truss.Application`
- `Microsoft.EntityFrameworkCore`
- `Microsoft.Extensions.Diagnostics.HealthChecks`

---

## Truss.AspNetCore

**Purpose:**
Endpoint mapping for commands and queries, with automatic ProblemDetails responses for validation and business rule failures.

### Dependencies

- `Truss.Application.Abstractions`
- ASP.NET Core shared framework (framework reference, not a package)

---

## Truss.Generators

**Purpose:**
Compile-time handler discovery, dispatch priming and build diagnostics. Development dependency: nothing is added to the published application.

### Dependencies

None at runtime. Referenced with `PrivateAssets="all"` in the composition root project.

---

## Truss.Messaging.Abstractions

**Purpose:**
Contracts for integration events: the event marker and base record, the handler, the publisher and the naming attribute with versioning.

### Dependencies

None.

---

## Truss.Messaging

**Purpose:**
Integration event runtime: versioned JSON serialization, outbox model and processor, transport abstraction, consumer dispatch and the in-memory transport.

### Dependencies

- `Truss.Messaging.Abstractions`
- `Truss.Application.Abstractions`
- `Microsoft.Extensions.Diagnostics.HealthChecks`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Hosting.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`
- `Microsoft.Extensions.Options`

---

## Truss.Messaging.EntityFrameworkCore

**Purpose:**
Outbox and inbox persistence through EF Core: the model configurations and stores that join the message's unit of work.

### Dependencies

- `Truss.Messaging`
- `Microsoft.EntityFrameworkCore.Relational`

---

## Truss.Messaging.AspNetCore

**Purpose:**
Operational endpoints for the outbox: counters and dead-letter retry.

### Dependencies

- `Truss.Messaging`
- ASP.NET Core shared framework (framework reference, not a package)

---

## Truss.Messaging.Postgres

**Purpose:**
Durable Postgres transport: table-backed queue with SKIP LOCKED competing consumers, LISTEN/NOTIFY wake-up, retry with backoff and a dead-letter table.

### Dependencies

- `Truss.Messaging`
- `Npgsql`

---

## Truss.Messaging.RabbitMq

**Purpose:**
Durable RabbitMQ transport: quorum queues with publisher confirms, broker-side delivery accounting and dead-lettering.

### Dependencies

- `Truss.Messaging`
- `RabbitMQ.Client`

---

## Truss.Messaging.Redis

**Purpose:**
Durable Redis transport: Streams with consumer groups, pending message reclaim and a dead-letter stream.

### Dependencies

- `Truss.Messaging`
- `StackExchange.Redis`

---

## Truss.Email.Abstractions

**Purpose:**
Contracts for sending email: the message shape and the sender abstraction.

### Dependencies

None.

---

## Truss.Email

**Purpose:**
Email senders and address validation: SMTP through MailKit, a console sender for development, and a validator combining RFC syntax with a DNS deliverability check.

### Dependencies

- `Truss.Email.Abstractions`
- `MailKit`
- `DnsClient`

---

## Truss.Email.Resend

**Purpose:**
Resend provider: delivery through the official API client behind the sender abstraction.

### Dependencies

- `Truss.Email.Abstractions`
- `Resend`

---

## Truss.Tenancy.Abstractions

**Purpose:**
The ambient tenant context of the current request.

### Dependencies

None.

---

## Truss.Tenancy.EntityFrameworkCore

**Purpose:**
Row-level tenant isolation: the tenant-owned marking, the shadow column with its filter, and the stamp on save.

### Dependencies

- `Truss.Tenancy.Abstractions`
- `Microsoft.EntityFrameworkCore`

---

## Truss.Tenancy.AspNetCore

**Purpose:**
HTTP tenant resolution from claims, headers or a custom resolver.

### Dependencies

- `Truss.Tenancy.Abstractions`

---

## Truss.Rbac

**Purpose:**
Role-based access control over standard authorization: roles defined in code, on-demand permission policies and per-request role claims enrichment.

### Dependencies

None beyond the shared framework.

---

## Truss.Rbac.EntityFrameworkCore

**Purpose:**
Role assignments persisted in the application database.

### Dependencies

- `Truss.Rbac`
- `Microsoft.EntityFrameworkCore.Relational`

---

## Truss.Observability.OpenTelemetry

**Purpose:**
OTLP bridge: exports the Truss activity sources, meter and application logs through the OpenTelemetry SDK, with ASP.NET Core and HttpClient instrumentation.

### Dependencies

- `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- `OpenTelemetry.Extensions.Hosting`
- `OpenTelemetry.Instrumentation.AspNetCore`
- `OpenTelemetry.Instrumentation.Http`

---

## Truss.Testing

**Purpose:**
Integration test host: boots the pipeline, a throwaway sqlite database, the in-memory transport with the outbox and the job runtime, with helpers for sending requests, draining the outbox deterministically and awaiting jobs.

### Dependencies

- `Truss.Application`
- `Truss.Persistence.EntityFrameworkCore`
- `Truss.Messaging` and `Truss.Messaging.EntityFrameworkCore`
- `Truss.Jobs` and `Truss.Jobs.EntityFrameworkCore`
- `Microsoft.EntityFrameworkCore.Sqlite`

---

## Truss.Jobs.Abstractions

**Purpose:**
Contracts for background jobs: the job interface, the execution context, the scheduler, the monitor and the naming attribute.

### Dependencies

None.

---

## Truss.Jobs

**Purpose:**
Job runtime: transactional enqueueing through the outbox, the executor with retry and timeout, scheduled and recurring jobs, and the in-memory store.

### Dependencies

- `Truss.Jobs.Abstractions`
- `Truss.Messaging`
- `Microsoft.Extensions.Diagnostics.HealthChecks`
- `Cronos`

---

## Truss.Jobs.EntityFrameworkCore

**Purpose:**
Job records persisted through EF Core, scheduled atomically with the command that enqueued them.

### Dependencies

- `Truss.Jobs`
- `Microsoft.EntityFrameworkCore.Relational`

---

## Truss.Jobs.AspNetCore

**Purpose:**
Progress endpoints: snapshot polling and server-sent events streaming.

### Dependencies

- `Truss.Jobs.Abstractions`
- ASP.NET Core shared framework (framework reference, not a package)

---

## Truss.Observability

**Purpose:**
Structured request logging, spans and metrics through BCL diagnostics, and the ambient execution context.

### Dependencies

- `Truss.Application.Abstractions`
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Logging.Abstractions`

---

## Truss.Observability.AspNetCore

**Purpose:**
Correlation middleware: reads or creates the correlation id per request and echoes it back.

### Dependencies

- `Truss.Observability`
- ASP.NET Core shared framework (framework reference, not a package)

---

## Truss.Cli

**Purpose:**
The `truss` command line tool: project scaffolding, manifest-driven module installation, code generation and project verification.

### Dependencies

- `Spectre.Console`
- `Spectre.Console.Cli`

Installed as a dotnet global tool; never referenced by application code.

---

## Truss.Mapping

**Purpose:**
Compile-time DTO mapping: mapper implementations generated from partial method declarations. Development dependency: nothing is added to the published application.

### Dependencies

None at runtime. Referenced with `PrivateAssets="all"`, usually in the application layer.

---

## Truss.Auth.Abstractions

**Purpose:**
Contracts for authentication mechanics: password hashing and token issuing, referenced by the application layer.

### Dependencies

None.

---

## Truss.Auth.Jwt

**Purpose:**
JWT authentication mechanics: PBKDF2 password hashing, access and refresh token issuing, JwtBearer wiring.

### Dependencies

- `Truss.Auth.Abstractions`
- `Microsoft.AspNetCore.Authentication.JwtBearer`
- ASP.NET Core shared framework (framework reference, not a package)
