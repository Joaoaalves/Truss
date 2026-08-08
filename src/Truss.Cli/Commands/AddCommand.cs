using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal sealed class AddCommand : Command<AddCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<module>")]
            public string Module { get; init; } = string.Empty;

            [CommandOption("--transport <TRANSPORT>")]
            public string? Transport { get; init; }

            [CommandOption("--provider <PROVIDER>")]
            public string? Provider { get; init; }

            [CommandOption("--dashboard <DASHBOARD>")]
            public string? Dashboard { get; init; }

            [CommandOption("--bind-user <AGGREGATE>")]
            public string? BindUser { get; init; }

            [CommandOption("--bind-mode <MODE>")]
            public string? BindMode { get; init; }

            [CommandOption("--external <PROVIDERS>")]
            public string? External { get; init; }

            [CommandOption("--project <PATH>")]
            public string? Project { get; init; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var located = TrussManifest.Locate(settings.Project ?? Directory.GetCurrentDirectory());

            if (located is not { } project)
            {
                Console.WriteLine("No truss.json found. Run this command inside a project created with truss new, or pass --project.");
                return 1;
            }

            var module = settings.Module.ToLowerInvariant();

            var option = module switch
            {
                "auth" or "email" => settings.Provider,
                "observability" => settings.Dashboard,
                _ => settings.Transport
            };

            var auth = module == "auth"
                ? new AuthAddOptions(
                    settings.BindUser,
                    settings.BindMode?.ToLowerInvariant(),
                    settings.External?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
                : null;

            return ModuleInstaller.Install(
                module,
                option?.ToLowerInvariant(),
                project.Manifest,
                project.Root,
                Console.WriteLine,
                auth);
        }
    }
}
