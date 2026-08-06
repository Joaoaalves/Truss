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

Unless `--empty` is passed, a sample `Catalog` bounded context shows the full pattern in working code: a `Product` aggregate with a typed id, a business rule and a domain event; `CreateProduct` and `GetProductById` with handlers, validator and repository; the EF configuration converting the typed id; and the endpoints mapped with `MapCommand` and `MapQuery`. Delete the folders when you are done reading them.

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
| `auth` | `--provider jwt`, `identity` | Scaffolds the editable Accounts context and wires JWT authentication; the identity provider runs credentials through ASP.NET Core Identity; requires a database |

---

## truss generate

```
truss generate context Sales
truss generate aggregate Order --context Sales
truss generate command PlaceOrder --context Sales
truss generate query GetOrderById --context Sales --result OrderDto
```

Generates building blocks inside the layer projects, following the folder-per-context layout: `Domain/Sales`, `Application/Sales`. Aggregates come with their typed id and creation event; commands come with handler and validator; queries with handler. Existing files are never overwritten.

---

## truss dev

```
truss dev
```

The development loop in one command: starts the compose dependencies when the project has them (`docker compose up -d --wait`), prints the URLs that matter (API, Scalar, jobs, dashboard, RabbitMQ management, depending on what is installed) and runs the API through `dotnet watch` with hot reload until Ctrl+C. `--no-docker` skips the compose step.

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
