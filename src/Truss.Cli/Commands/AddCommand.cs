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

            return ModuleInstaller.Install(
                settings.Module.ToLowerInvariant(),
                settings.Transport?.ToLowerInvariant(),
                project.Manifest,
                project.Root,
                Console.WriteLine);
        }
    }
}
