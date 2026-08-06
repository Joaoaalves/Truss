using System.Text;

namespace Truss.Cli
{
    /// <summary>
    /// Generates AGENTS.md from the manifest: the project-level guidance file that
    /// coding agents read before touching the code. The truss-owned section lives
    /// between markers and is regenerated when modules change; everything outside
    /// the markers belongs to the user and is preserved.
    /// </summary>
    internal static class AgentsGenerator
    {
        private const string BeginMarker = "<!-- truss:begin -->";
        private const string EndMarker = "<!-- truss:end -->";

        public static void Write(TrussManifest manifest, string rootDirectory)
        {
            var path = Path.Combine(rootDirectory, "AGENTS.md");
            var block = BuildManagedBlock(manifest);

            if (!File.Exists(path))
            {
                File.WriteAllText(path, $"# {manifest.Name}{Environment.NewLine}{Environment.NewLine}{block}{Environment.NewLine}");
                return;
            }

            var content = File.ReadAllText(path);
            var begin = content.IndexOf(BeginMarker, StringComparison.Ordinal);
            var end = content.IndexOf(EndMarker, StringComparison.Ordinal);

            if (begin < 0 || end < begin)
                return;

            File.WriteAllText(path, content[..begin] + block + content[(end + EndMarker.Length)..]);
        }

        private static string BuildManagedBlock(TrussManifest manifest)
        {
            var name = manifest.Name;
            var block = new StringBuilder();

            block.AppendLine(BeginMarker);
            block.AppendLine($"""
                This project is built on Truss, a modular .NET framework for DDD and CQRS.
                The truss CLI regenerates this section when modules change; write custom
                guidance outside the markers.

                ## Layout

                - src/{name}.Domain: aggregates, value objects, domain events and business rules. No dependencies beyond Truss.Domain; never reference infrastructure here.
                - src/{name}.Application: commands, queries, handlers and validators, organized by bounded context folder.
                - src/{name}.Infrastructure: persistence models, EF configurations, repositories and module registrations.
                - src/{name}.Api: the composition root. Program.cs wires modules and maps endpoints.

                ## Rules that gate every change

                - Handlers orchestrate; aggregates decide. Enforce invariants with business rules inside the domain (CheckRule), not in handlers.
                - Commands and queries are records dispatched through the pipeline. Map them to routes with MapCommand and MapQuery; routes are always explicit.
                - Never inject IUnitOfWork into handlers. The pipeline commits after the handler succeeds; a thrown exception rolls everything back.
                - Validators check input shape and become 400 responses with every failure listed. Business rule violations become 422. Do not convert one into the other.
                - Authentication state (hashes, tokens) never lives on domain entities; it belongs to infrastructure models behind application store interfaces.
                - Handlers return ids or DTOs, never entities.

                ## Workflow

                - New bounded context: truss generate context <Name>
                - New aggregate: truss generate aggregate <Name> --context <Context>
                - New command or query: truss generate command|query <Name> --context <Context> (record, handler and validator; map it in Program.cs)
                - Install a module: truss add messaging|jobs|observability|mapping|auth
                - Evolve the schema: truss db add <Name>, then truss db migrate (development applies pending migrations on startup)
                - Run locally: truss dev (starts docker dependencies and watches the API with hot reload)
                - Verify wiring after structural changes: truss doctor
                - Build and test: dotnet build and dotnet test on {name}.slnx.
                """);

            block.AppendLine("## Installed modules");
            block.AppendLine();

            AppendInfrastructure(block, manifest);
            AppendModules(block, manifest);

            block.AppendLine();
            block.AppendLine("""
                ## Documentation

                Full docs: https://joaoaalves.github.io/Truss/ with a machine-readable index
                at /llms.txt and the complete text at /llms-full.txt.
                """);
            block.Append(EndMarker);

            return block.ToString();
        }

        private static void AppendInfrastructure(StringBuilder block, TrussManifest manifest)
        {
            if (manifest.UsesEntityFramework)
                block.AppendLine($"- Database: {manifest.Database} through EF Core. AppDbContext lives in the infrastructure project; configurations are applied from that assembly. The database is created with EnsureCreated in Development.");

            if (manifest.Docker)
                block.AppendLine("- Docker: docker-compose.yml carries the local dependencies; it is regenerated from truss.json, so do not edit it by hand. Start with docker compose up.");
        }

        private static void AppendModules(StringBuilder block, TrussManifest manifest)
        {
            manifest.Settings.TryGetValue("messaging.transport", out var transport);
            manifest.Settings.TryGetValue("observability.dashboard", out var dashboard);
            manifest.Settings.TryGetValue("auth.provider", out var authProvider);
            manifest.Settings.TryGetValue("email.provider", out var emailProvider);

            foreach (var module in manifest.Modules)
            {
                switch (module)
                {
                    case "messaging":
                        block.AppendLine($"- Messaging ({transport ?? "inmemory"} transport): publish integration events with IIntegrationEventPublisher inside command handlers; the outbox stores them atomically with the command. Consume with IIntegrationEventHandler<TEvent>. Give every cross-context event a stable name with [IntegrationEventName(\"context.event-name\")]. Delivery is at-least-once; make handlers idempotent.");
                        break;

                    case "jobs":
                        block.AppendLine("- Jobs: enqueue with IJobScheduler.Enqueue<TJob, TArgs> inside a handler (transactional). Implement IJob<TArgs>, report progress through JobContext, honor the CancellationToken. Track jobs at GET /truss/jobs/{id}, stream at /stream, cancel with POST /cancel.");
                        break;

                    case "observability":
                        block.AppendLine(dashboard is null
                            ? "- Observability: requests, messages and jobs are logged and traced automatically; correlation flows through the X-Correlation-Id header."
                            : $"- Observability: requests, messages and jobs are logged and traced automatically; correlation flows through X-Correlation-Id. Signals export over OTLP to the {dashboard} dashboard ({DashboardUrl(dashboard)} after docker compose up).");
                        break;

                    case "mapping":
                        block.AppendLine("- Mapping: declare DTO mappers as partial methods in a static partial class marked [Mapper]; the generator fills them in at compile time and unmapped members fail the build.");
                        break;

                    case "auth":
                        block.AppendLine($"- Auth ({authProvider ?? "jwt"} provider): endpoints /auth/register, /auth/login and /auth/refresh. The Accounts context is editable project code; extend the User aggregate freely but keep credentials out of it. Protect endpoints with .RequireAuthorization(); the sub claim carries the user id.");
                        break;

                    case "worker":
                        block.AppendLine($"- Worker: src/{manifest.Name}.Worker is a separate consumer process sharing the application and infrastructure layers; it competes for messages and jobs with the API. New modules installed later must be wired into its Program.cs by hand.");
                        break;

                    case "email":
                        block.AppendLine(emailProvider == "smtp"
                            ? "- Email: inject IEmailSender in handlers; delivery goes over SMTP (Mailpit at http://localhost:8025 in development). Send from integration event handlers or jobs so delivery inherits retry."
                            : "- Email: inject IEmailSender in handlers; messages print to the console log in development. Send from integration event handlers or jobs so delivery inherits retry.");
                        break;
                }
            }
        }

        private static string DashboardUrl(string dashboard) => dashboard switch
        {
            "aspire" => "http://localhost:18888",
            "grafana" => "http://localhost:3000",
            _ => "http://localhost:8081"
        };
    }
}
