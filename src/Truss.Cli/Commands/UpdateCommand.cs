using System.Diagnostics;
using System.Text.RegularExpressions;
using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal sealed partial class UpdateCommand : Command<UpdateCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-p|--project <PATH>")]
            public string? Project { get; init; }

            [CommandOption("--no-restore")]
            public bool NoRestore { get; init; }
        }

        [GeneratedRegex("""(<PackageReference\s+Include="Truss\.[^"]*"\s+Version=")([^"]+)(")""")]
        private static partial Regex TrussReference();

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var located = TrussManifest.Locate(settings.Project ?? Directory.GetCurrentDirectory());

            if (located is not { } project)
            {
                Console.WriteLine("No truss.json found. Run this command inside a project created with truss new, or pass --project.");
                return 1;
            }

            var version = TrussVersionInfo.Current();
            var updated = 0;

            foreach (var csproj in Directory.EnumerateFiles(Path.Combine(project.Root, "src"), "*.csproj", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(csproj);
                var replaced = TrussReference().Replace(content, match =>
                    match.Groups[2].Value == version ? match.Value : $"{match.Groups[1].Value}{version}{match.Groups[3].Value}");

                if (replaced == content)
                    continue;

                File.WriteAllText(csproj, replaced);
                updated++;
                Console.WriteLine($"Updated {Path.GetRelativePath(project.Root, csproj)}");
            }

            project.Manifest.TrussVersion = version;
            project.Manifest.Save(project.Root);

            Console.WriteLine(updated == 0
                ? $"Every Truss package already targets {version}."
                : $"Truss packages now target {version}.");

            if (settings.NoRestore)
            {
                Console.WriteLine("Skipping the restore. If it fails with NU1102 for a version that is already published, run: dotnet restore --no-http-cache");
                return 0;
            }

            return Restore(project.Root, version, cancellationToken);
        }

        /// <summary>
        /// Restores without the http cache. NuGet caches the list of versions of
        /// a package for half an hour, so a release published minutes ago is
        /// invisible to a plain restore and fails with NU1102 even though the
        /// package is indexed. This command just moved the project to a version,
        /// so it is the right place to sidestep that.
        /// </summary>
        private static int Restore(string root, string version, CancellationToken cancellationToken)
        {
            Console.WriteLine("Restoring without the http cache, so a freshly published version is not missed.");

            using var process = Process.Start(new ProcessStartInfo("dotnet", "restore --no-http-cache")
            {
                WorkingDirectory = root,
                UseShellExecute = false
            });

            if (process is null)
            {
                Console.WriteLine("Could not start dotnet. Restore yourself with: dotnet restore --no-http-cache");
                return 1;
            }

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

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"The restore failed. If {version} was published in the last few minutes, wait for nuget.org to finish indexing and run it again.");
                return process.ExitCode;
            }

            Console.WriteLine("Build the solution and review the release notes for behavior changes.");
            return 0;
        }
    }
}
