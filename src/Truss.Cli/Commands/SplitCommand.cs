using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal sealed class SplitCommand : Command<SplitCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<context>")]
            public string Context { get; init; } = string.Empty;

            [CommandOption("--shared-database")]
            public bool SharedDatabase { get; init; }

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

            try
            {
                return ServiceSplitter.Split(project.Manifest, project.Root, settings.Context, settings.SharedDatabase, Console.WriteLine);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                Console.WriteLine(exception.Message);
                return 1;
            }
        }
    }
}
