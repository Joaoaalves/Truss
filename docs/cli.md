# The truss CLI

The `truss` command line scaffolds projects, installs modules over time and generates building blocks. It is a dotnet global tool:

```
dotnet tool install -g Truss.Cli
```

Everything the CLI knows about a project lives in `truss.json` at the repository root: the framework version, the chosen database, and every module installed with its settings. That manifest is what makes `truss add` work months after the project started.

---

## truss new

```
truss new MyShop --database postgres --docker
```

Scaffolds a complete Clean Architecture solution. Run without flags in a terminal for interactive prompts.

| Option | Values | Effect |
|---|---|---|
| `--database` | `postgres`, `sqlserver`, `sqlite`, `none` | EF Core provider, connection string and compose service. `none` skips the Infrastructure project entirely |
| `--docker` | flag | Generates `docker-compose.yml` for the chosen infrastructure |
| `--empty` | flag | Skips the sample bounded context |
| `--output` | path | Where to create the project. Defaults to the current directory |

The generated solution:

```
MyShop/
  truss.json
  MyShop.slnx
  docker-compose.yml
  src/
    MyShop.Domain/           references Truss.Domain only
    MyShop.Application/      references Truss.Application.Abstractions and FluentValidation
    MyShop.Infrastructure/   EF Core, the provider and AppDbContext
    MyShop.Api/              composition root with Truss.AspNetCore and Truss.Generators
```

Unless `--empty` is passed, a sample `Catalog` bounded context shows the full pattern in working code: a `Product` aggregate with a typed id, a business rule and a domain event; `CreateProduct` and `GetProductById` with handlers, validator and repository; the EF configuration converting the typed id; a `CatalogSeeder` planting development data; and the endpoints mapped with `MapCommand` and `MapQuery`. Delete the folders when you are done reading them.

Development data comes from seeders: classes implementing `ITrussSeeder`, registered with `AddTrussSeeder<T>()` and executed by `app.Services.RunTrussSeeders()`, which the scaffolded Program calls in development right after the schema is ready. Seeders run in registration order and should check before inserting, so restarting the application never duplicates data.

```
cd MyShop
docker compose up -d
dotnet run --project src/MyShop.Api
```

---

## truss add

```
truss add messaging --transport redis
truss add jobs
truss add observability
```

Installs a module into an existing project: adds the package references to the correct layer, wires the registrations into `Program.cs` and the model configuration into `AppDbContext.cs`, updates `docker-compose.yml` when infrastructure is needed, and records everything in the manifest. When a file was restructured and the anchor cannot be found, the CLI prints exactly what to paste instead of guessing.

| Module | Options | Notes |
|---|---|---|
| `messaging` | `--transport inmemory`, `postgres`, `rabbitmq`, `redis` | With a database, the outbox is installed automatically |
| `jobs` | | Requires messaging |
| `observability` | `--dashboard aspire`, `grafana`, `seq` | Logging, tracing and correlation; the dashboard option wires OTLP export and the compose service |
| `mapping` | | Compile-time DTO mappers, added to the application layer |
| `auth` | `--provider jwt`, `identity`; `--bind-user <Aggregate>` with `--bind-mode reference`, `merge`; `--external google,microsoft,github` | Scaffolds the editable Accounts context and wires JWT authentication; the identity provider runs credentials through ASP.NET Core Identity; the binding connects the account to an existing aggregate and the external option wires OAuth login providers (addable later too); requires a database |
| `email` | `--provider console`, `smtp`, `resend` | IEmailSender for the application layer; smtp brings Mailpit to the compose file, resend delivers through the API |
| `tenancy` | | Row-level tenant isolation: ambient resolution, query filtering and stamping; requires a database |
| `rbac` | | Roles in code, permissions on endpoints and assignments in the database; requires a database |
| `worker` | | Scaffolds src/Name.Worker, a separate consumer process wired with the installed modules; requires messaging |

---

## truss generate

```
truss generate context Sales
truss generate aggregate Order --context Sales
truss generate aggregate Invoice --context Billing --crud
truss generate entity OrderItem --context Sales --aggregate Order
truss generate command PlaceOrder --context Sales
truss generate query GetOrderById --context Sales --result OrderDto
truss generate query ListOrders --context Sales --result OrderDto --paged
```

Generates building blocks inside the layer projects. An aggregate gets its own folder with everything that belongs to it:

```
Domain/Sales/Order/
  Order.cs                    the aggregate, with the starter rule wired in Create
  ValueObjects/OrderId.cs     the typed id
  Events/OrderCreated.cs      the creation event
  Rules/OrderMustBeValid.cs   a starter rule to replace with the first real invariant
```

`generate entity` creates an `Entity<TId>` with its typed id, in its own folder or nested inside an owning aggregate's folder with `--aggregate`. Namespaces stay flat per context; the folders only organize.

`--crud` on an aggregate generates the full vertical slice: `Create`, `Update` and `Delete` commands with handlers and validators, `GetById` and a paged `List` query, the repository interface in the application layer with its EF implementation and configuration in infrastructure, the repository registration and the five routes wired into `Program.cs`. The generated aggregate carries a starter `Name` field so everything works end to end immediately; `Update` goes through an intention-revealing `Rename` method on the aggregate, showing where real behavior belongs instead of property setters. Missing records surface as a 422 with the stable code `<name>.not-found`.

Existing files are never overwritten.

---

## truss dev

```
truss dev
```

The development loop in one command: starts the compose dependencies when the project has them (`docker compose up -d --wait`), prints the URLs that matter (API, Scalar, health, jobs, dashboard, RabbitMQ management, depending on what is installed) and runs the API through `dotnet watch` with hot reload until Ctrl+C. `--no-docker` skips the compose step.

---

## truss db

```
truss db add InitialCreate
truss db migrate
```

EF Core migrations without remembering the project layout: `db add` captures the current model changes into a migration in the infrastructure project, and `db migrate` applies pending migrations to the database. Both restore `dotnet-ef` from the tool manifest the scaffold carries (`.config/dotnet-tools.json`) and pass the right `--project` and `--startup-project` for you.

In development the scaffolded startup applies pending migrations automatically once the first migration exists; before that, it falls back to `EnsureCreated`, so the day-one experience stays untouched. In production, run `truss db migrate` (or `dotnet ef database update`) as a deploy step; the application never migrates on its own outside development.

---

## truss update

```
truss update
```

Points every `Truss.*` package reference in the project at the CLI's own version and records it in the manifest. Update the CLI first (`dotnet tool update -g Truss.Cli`), run `truss update`, build, and review the release notes for behavior changes.

---

## truss doctor

```
truss doctor
```

Compares the manifest with the actual state of the project: solution and projects present, module packages referenced, compose file in place. Prints one line per check and fails with a non-zero exit code when something is missing.

---

## AGENTS.md

Every scaffolded project carries an `AGENTS.md` at the root: the guidance file coding agents read before touching the code. It describes the layout, the architecture rules that gate every change, the CLI workflow and each installed module with its usage pattern, and it points at the machine-readable docs (`llms.txt`).

The truss-owned section lives between `<!-- truss:begin -->` and `<!-- truss:end -->` markers and is regenerated whenever `truss add` changes the project, so it always reflects the modules actually installed. Everything outside the markers is yours: add team conventions freely and they survive regeneration. Deleting the markers opts the file out of regeneration entirely.

---

## Notes

- Commands that operate on an existing project locate `truss.json` upward from the current directory; `--project <path>` overrides.
- The scaffolded packages pin the CLI's own version, so project and framework stay in step.
- `--local-packages <path>` adds a local NuGet source to the scaffold, useful for testing unreleased framework builds.
