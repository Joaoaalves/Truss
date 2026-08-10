using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal sealed class RemoveContextCommand : Command<RemoveContextCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<name>")]
            public string Name { get; init; } = string.Empty;

            [CommandOption("-p|--project <PATH>")]
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

            return ContextRemover.Remove(project.Manifest, project.Root, settings.Name, Console.WriteLine);
        }
    }
}
