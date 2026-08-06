using System.Diagnostics;
using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal abstract class DbCommandBase<TSettings> : Command<TSettings>
        where TSettings : DbCommandBase<TSettings>.DbSettings
    {
        public class DbSettings : CommandSettings
        {
            [CommandOption("--project <PATH>")]
            public string? Project { get; init; }
        }

        protected sealed override int Execute(CommandContext context, TSettings settings, CancellationToken cancellationToken)
        {
            var located = TrussManifest.Locate(settings.Project ?? Directory.GetCurrentDirectory());

            if (located is not { } project)
            {
                Console.WriteLine("No truss.json found. Run this command inside a project created with truss new, or pass --project.");
                return 1;
            }

            if (!project.Manifest.UsesEntityFramework)
            {
                Console.WriteLine("The project has no database. Scaffold with --database to use migrations.");
                return 1;
            }

            EnsureToolManifest(project.Root);

            var restore = Run("dotnet", "tool restore", project.Root, cancellationToken);

            if (restore != 0)
            {
                Console.WriteLine("dotnet tool restore failed; dotnet-ef could not be prepared.");
                return restore;
            }

            var arguments =
                $"ef {EfArguments(settings)} " +
                $"--project {project.Manifest.InfrastructureProject} " +
                $"--startup-project {project.Manifest.ApiProject}";

            return Run("dotnet", arguments, project.Root, cancellationToken);
        }

        protected abstract string EfArguments(TSettings settings);

        private static void EnsureToolManifest(string root)
        {
            var path = Path.Combine(root, ".config", "dotnet-tools.json");

            if (File.Exists(path))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Templates.ProjectTemplates.ToolsManifest + Environment.NewLine);
            Console.WriteLine("A tool manifest with dotnet-ef was added at .config/dotnet-tools.json.");
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

    internal sealed class DbAddCommand : DbCommandBase<DbAddCommand.Settings>
    {
        public sealed class Settings : DbSettings
        {
            [CommandArgument(0, "<name>")]
            public string Name { get; init; } = string.Empty;
        }

        protected override string EfArguments(Settings settings)
        {
            return $"migrations add {settings.Name}";
        }
    }

    internal sealed class DbMigrateCommand : DbCommandBase<DbMigrateCommand.Settings>
    {
        public sealed class Settings : DbSettings
        {
        }

        protected override string EfArguments(Settings settings)
        {
            return "database update";
        }
    }
}
