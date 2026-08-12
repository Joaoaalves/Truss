using System.Diagnostics;
using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal sealed class DevCommand : Command<DevCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-p|--project <PATH>")]
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

            if (plan.Hosts.Count == 1)
            {
                Console.WriteLine("Watching for changes. Press Ctrl+C to stop.");
                Console.WriteLine();

                return Run("dotnet", "watch run", plan.ApiProjectPath, cancellationToken);
            }

            Console.WriteLine($"Starting the constellation: {string.Join(", ", plan.Hosts.Select(host => host.Label))}.");

            if (project.Manifest.Modules.Contains("observability"))
                Console.WriteLine("One trace follows a request across every host; the dashboard shows it end to end.");

            Console.WriteLine("Watching for changes. Press Ctrl+C to stop everything.");
            Console.WriteLine();

            return RunConstellation(plan, cancellationToken);
        }

        /// <summary>
        /// Runs every host of the constellation under dotnet watch, one process
        /// each, with the output interleaved and prefixed by the host's name.
        /// Stopping stops all of them.
        /// </summary>
        private static int RunConstellation(DevPlan plan, CancellationToken cancellationToken)
        {
            var width = plan.Hosts.Max(host => host.Label.Length);
            var gate = new object();
            var processes = new List<Process>();

            foreach (var host in plan.Hosts)
            {
                var start = new ProcessStartInfo("dotnet", "watch run --non-interactive")
                {
                    WorkingDirectory = host.ProjectPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                start.Environment["DOTNET_WATCH_SUPPRESS_EMOJIS"] = "1";

                var process = Process.Start(start);

                if (process is null)
                {
                    Console.WriteLine($"Could not start {host.Label}.");
                    continue;
                }

                var prefix = $"[{host.Label.PadRight(width)}]";

                void Forward(object sender, DataReceivedEventArgs args)
                {
                    if (args.Data is null)
                        return;

                    lock (gate)
                    {
                        Console.WriteLine($"{prefix} {args.Data}");
                    }
                }

                process.OutputDataReceived += Forward;
                process.ErrorDataReceived += Forward;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                processes.Add(process);
            }

            if (processes.Count == 0)
                return 1;

            using var registration = cancellationToken.Register(() =>
            {
                foreach (var process in processes)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
            });

            foreach (var process in processes)
                process.WaitForExit();

            return processes.Max(process => process.ExitCode);
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
