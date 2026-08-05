using Spectre.Console;
using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal sealed class NewCommand : Command<NewCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[name]")]
            public string? Name { get; init; }

            [CommandOption("--database <DATABASE>")]
            public string? Database { get; init; }

            [CommandOption("--docker")]
            public bool Docker { get; init; }

            [CommandOption("--empty")]
            public bool Empty { get; init; }

            [CommandOption("--output <PATH>")]
            public string? Output { get; init; }

            [CommandOption("--local-packages <PATH>")]
            public string? LocalPackages { get; init; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var interactive = AnsiConsole.Profile.Capabilities.Interactive;

            var name = settings.Name;

            if (name is null)
            {
                if (!interactive)
                {
                    Console.WriteLine("A project name is required. Usage: truss new <name> --database <postgres|sqlserver|sqlite|none>");
                    return 1;
                }

                name = AnsiConsole.Prompt(new TextPrompt<string>("Project name:"));
            }

            var database = settings.Database;

            if (database is null)
            {
                if (!interactive)
                {
                    Console.WriteLine("A database is required. Use --database with one of: postgres, sqlserver, sqlite, none.");
                    return 1;
                }

                database = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("Which database?")
                    .AddChoices(ProjectScaffolder.Databases));
            }

            var docker = settings.Docker || (interactive && !settings.Docker && database is "postgres" or "sqlserver"
                && AnsiConsole.Prompt(new ConfirmationPrompt("Generate docker-compose.yml for the database?")));

            var sample = !settings.Empty && database != "none";

            if (!settings.Empty && database == "none")
                Console.WriteLine("No database selected; scaffolding without the sample bounded context.");

            try
            {
                var root = ProjectScaffolder.Scaffold(new ScaffoldOptions(
                    name,
                    database,
                    docker,
                    sample,
                    settings.Output ?? Directory.GetCurrentDirectory(),
                    settings.LocalPackages));

                Console.WriteLine($"Project {name} created at {root}");
                Console.WriteLine();
                Console.WriteLine("Next steps:");
                Console.WriteLine($"  cd {name}");

                if (docker)
                    Console.WriteLine("  docker compose up -d");

                Console.WriteLine($"  dotnet run --project src/{name}.Api");

                if (sample)
                {
                    Console.WriteLine();
                    Console.WriteLine("Try the sample:");
                    Console.WriteLine("  POST /products with {\"name\": \"Beam\", \"price\": 10}");
                    Console.WriteLine("  GET  /products/{id}");
                }

                return 0;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                Console.WriteLine(exception.Message);
                return 1;
            }
        }
    }
}
