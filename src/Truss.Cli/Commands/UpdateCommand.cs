using System.Text.RegularExpressions;
using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    internal sealed partial class UpdateCommand : Command<UpdateCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--project <PATH>")]
            public string? Project { get; init; }
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
                : $"Truss packages now target {version}. Build the solution to restore, and review the release notes for behavior changes.");

            return 0;
        }
    }
}
