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
| `--sample` | flag | Includes the sample `Catalog` bounded context; the default scaffold is clean |
| `--no-tests` | flag | Skips the test projects; by default the scaffold carries `tests/<Name>.Domain.Tests` and `tests/<Name>.IntegrationTests` |
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

The default scaffold is clean: no example code to delete before the real work starts. With `--sample` (or answering yes to the interactive prompt), a `Catalog` bounded context shows the full pattern in working code, laid out exactly like generated code: a `Product` aggregate with its typed id, domain event and business rule in their folders; `CreateProduct` and `GetProductById` each in their own folder with handler and validator; the DTO in `DTOs/`; the repository, EF configuration and a `CatalogSeeder` in infrastructure; and the endpoints mapped with `MapCommand` and `MapQuery`. When you are done reading it, `truss remove context Catalog` takes all of it out.

Development data comes from seeders: classes implementing `ITrussSeeder`, registered with `AddTrussSeeder<T>()` and executed by `app.Services.RunTrussSeeders()`, which the scaffolded Program calls in development right after the schema is ready. Seeders run in registration order and should check before inserting, so restarting the application never duplicates data.

Two test projects come along by default: `tests/MyShop.Domain.Tests` for pure unit tests of aggregates and rules, and `tests/MyShop.IntegrationTests` dispatching commands through the full pipeline on the [TrussTestHost](testing.md), with a smoke test proving the host boots. Generators add matching tests as the code grows. `--no-tests` skips them; `truss add tests` brings them later.

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
| `tests` | | Scaffolds the two test projects into an existing project and adds them to the solution; with the sample present, its tests come too |
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

These are the commands you type most, so each one answers to a short alias: `g` (or `gen`) for `generate`, and `ctx`, `agg`, `ent`, `vo`, `cmd`, `qry` for the building blocks. `truss rm ctx` is `truss remove context`, and the frequent options have short forms too: `-c` for `--context`, `-a` for `--aggregate`, `-r` for `--result`, `-f` for `--field`, `-p` for `--project`. The same line twice:

```
truss generate aggregate Invoice --context Billing --crud
truss g agg Invoice -c Billing --crud
```

### Value objects with invariants

`--vo Name:type:rules` (repeatable, or a comma list) replaces primitives with self-validating value objects. Supported types: `string`, `int`, `long`, `decimal`, `double`, `guid`; the type defaults to `string`.

```
truss g agg Food -c Nutrition --crud --vo Name:string:3..120 --vo Calories:int:0..900 --vo Fat:decimal:pos
```

Every member of `Food` becomes a value object in its own folder, with a private constructor, a `Create` factory that normalizes and checks the rules, equality by value, and its own tests. Value objects have no identity and raise no events; if one exists, it is valid.

The rules segment borrows what you already type elsewhere. A range is inclusive, as in SQL BETWEEN; on a string a bound measures length, on a number it measures value, in the Laravel tradition; and the comparators are the ones of REST filters. Join tokens with `+`. Everything is shell-safe: no quoting needed.

| Segment | Meaning |
|---|---|
| (none) | strings: not empty, at most 200 chars; numbers: not negative; guids: not default |
| `3..120` | between 3 and 120, inclusive (`3..` and `..120` bound one side) |
| `gt=0`, `gte=0`, `lt=10`, `lte=10` | the usual comparators; `min=`/`max=` are synonyms of `gte=`/`lte=` |
| `pos` | greater than zero |
| `gt=0+lte=10` | combined |

Each token becomes a real named rule with a stable code (`FoodCaloriesMustBeAtMost`, `foodCalories.too-large`), and length bounds become constants on the value object (`FoodName.MinLength`, `FoodName.MaxLength`) that the generated validator and EF configuration reference, so the number lives in one place.

With `--crud`, the whole slice speaks the value objects while the boundary stays primitive: commands carry `string` and `int`, handlers convert through `Create`, the EF configuration gets the conversions automatically, and the generated integration test drives the slice end to end.

### Composed and shared value objects

`truss g vo` builds standalone value objects. With `-f` the members are primitive fields of one class; with `--vo` each member becomes a value object of its own and the class composes them:

```
truss g vo Money -c Shared -f Amount:decimal:pos -f Currency:string:3..3
truss g vo MacroNutrients -c Nutrition -a Food --vo Carbohydrates:decimal --vo Fat:decimal --vo Protein:decimal
```

The composite's members are generated beside it, each guarding itself; the composite gets a canonical `Create` over the member types, marked as the home for rules that read several members, and a primitive overload (`MacroNutrients.Create(28m, 0.3m, 2.7m)`) that keeps construction short while every invariant still runs. Behavior that derives from the members (a calories total, for one) is yours to write there; the CLI never generates domain methods.

`-a <Aggregate>` places the value object inside the owning aggregate's folder and prints the lines that wire it in. Without it, the value object lands in the context's shared `ValueObjects/` folder, where several aggregates can speak it.

An aggregate can also reference an existing value object by naming it as the type:

```
truss g agg Meal -c Nutrition --vo Name:string --vo Macros:MacroNutrients
```

Nothing is regenerated: the property is typed to the value object found in the domain. Referenced value objects are not yet flattened into `--crud` slices, and a multi-member value object maps as an EF complex type rather than a conversion, as shown in [the domain guide](domain.md#mapping-value-objects-with-ef-core).

Generates building blocks inside the layer projects. Namespaces mirror the folders exactly. An aggregate gets its own folder and namespace with everything that belongs to it:

```
Domain/Sales/Order/
  Order.cs                    the aggregate, namespace Shop.Domain.Sales.Order
  ValueObjects/OrderId.cs     the typed id, in .ValueObjects
  Events/OrderCreated.cs      the creation event, in .Events
  Rules/OrderMustBeValid.cs   a starter rule to replace with the first real invariant, in .Rules
```

`generate entity` creates an `Entity<TId>` with its typed id, in its own folder or nested inside an owning aggregate's folder (and namespace) with `--aggregate`. `generate command` and `generate query` put each command or query in its own folder with its handler and validator, under its own namespace.

`--crud` on an aggregate generates the full vertical slice, organized the same way:

```
Application/Billing/Invoice/
  IInvoiceRepository.cs       the repository contract
  DTOs/InvoiceDto.cs
  Rules/InvoiceMustExist.cs   surfaces as 422 with the stable code invoice.not-found
  CreateInvoice/              CreateInvoice.cs, handler and validator
  UpdateInvoice/              command, handler and validator
  DeleteInvoice/              command and handler
  GetInvoiceById/             query and handler
  ListInvoice/                paged query, handler and validator
```

The EF repository implementation and configuration land in infrastructure, and the repository registration plus the five routes are wired into `Program.cs`. The generated aggregate carries a starter `Name` field so everything works end to end immediately; `Update` goes through an intention-revealing `Rename` method on the aggregate, showing where real behavior belongs instead of property setters. Inside the application feature the using directives sit within the namespace, so the aggregate type always resolves over its same-named namespace.

When the project has the test projects, generated code arrives tested: an aggregate brings a domain test asserting its creation event (and, with `--crud`, the `Rename` behavior), and the crud slice brings an integration test driving create, read, update, list and delete through the pipeline. The tests mirror the context folders and are yours to grow.

Existing files are never overwritten.

---

## truss remove

```
truss remove context Catalog
```

Removes a bounded context: deletes its folders across the Domain, Application and Infrastructure projects and the test projects, sweeps loose files at the project roots that belong to the removed types (the tests, configuration and repository of a block generated without `--context`), and cleans the wiring that pointed at it, in `Program.cs`, the worker's `Program.cs`, `AppDbContext.cs` and the infrastructure module: usings of the context's namespaces and every line referencing one of the removed types. A slice generated with `--crud` unwinds completely, routes and registration included, and removing the sample `Catalog` also drops its `DbSet`, seeder registration and endpoints.

The `Accounts` context scaffolded by `truss add auth` is refused: it belongs to a module, and removing it would leave the module half-wired. If the project has migrations, the CLI reminds you to capture the schema change with `truss db add`.

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
