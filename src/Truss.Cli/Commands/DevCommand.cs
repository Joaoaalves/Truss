using System.Diagnostics;
using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal sealed class DevCommand : Command<DevCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--project <PATH>")]
            public string? Project { get; init; }

            [CommandOption("--no-docker")]
            public bool NoDocker { get; init; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var located = TrussManifest.Locate(settings.Project ?? Directory.GetCurrentDirectory());

            if (located is not { } project)
            {
                Console.WriteLine("No truss.json found. Run this command inside a project created with truss new, or pass --project.");
                return 1;
            }

            var plan = DevPlanner.Build(project.Manifest, project.Root);

            if (plan.RunCompose && !settings.NoDocker)
            {
                Console.WriteLine("Starting the local dependencies with docker compose...");

                if (Run("docker", "compose up -d --wait", project.Root, cancellationToken) != 0)
                {
                    Console.WriteLine("docker compose failed. Is docker running? Use --no-docker to skip it.");
                    return 1;
                }
            }

            Console.WriteLine();

            foreach (var url in plan.Urls)
                Console.WriteLine($"  {url.Label,-10} {url.Url}");

            Console.WriteLine();
            Console.WriteLine("Watching for changes. Press Ctrl+C to stop.");
            Console.WriteLine();

            return Run("dotnet", "watch run", plan.ApiProjectPath, cancellationToken);
        }

        private static int Run(string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
        {
            using var process = Process.Start(new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                UseShellExecute = false
            });

            if (process is null)
                return 1;

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                }
            });

            process.WaitForExit();
            return process.ExitCode;
        }
    }
}
