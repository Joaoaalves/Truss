using Spectre.Console.Cli;

namespace Truss.Cli.Commands
{
    /// <summary>
    /// Generates the deployment artifacts of a target. The CLI writes files
    /// the ecosystem's own tools apply; it never deploys anything itself.
    /// </summary>
    internal sealed class DeployInitCommand : Command<DeployInitCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<target>")]
            public string Target { get; init; } = string.Empty;

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

            if (settings.Target.ToLowerInvariant() != "ssh")
            {
                Console.WriteLine($"Unknown deployment target '{settings.Target}'. Available targets: ssh.");
                return 1;
            }

            return DeploySshScaffolder.Install(project.Manifest, project.Root, Console.WriteLine);
        }
    }

    /// <summary>
    /// Verifies that a deployment target carries every value the installed
    /// modules will demand at boot, before anything is deployed. The list
    /// comes from the manifest; the target is an env file or the current
    /// environment.
    /// </summary>
    internal sealed class DeployCheckCommand : Command<DeployCheckCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("--env-file <PATH>")]
            public string? EnvFile { get; init; }

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

            IReadOnlyDictionary<string, string> target;

            if (settings.EnvFile is { } envFile)
            {
                if (!File.Exists(envFile))
                {
                    Console.WriteLine($"No env file at {envFile}.");
                    return 1;
                }

                target = ParseEnvFile(envFile);
                Console.WriteLine($"Checking {envFile} against the manifest:");
            }
            else
            {
                target = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty);

                Console.WriteLine("Checking the current environment against the manifest:");
            }

            Console.WriteLine();

            var requirements = DeployRequirements.Build(project.Manifest);

            if (requirements.Count == 0)
            {
                Console.WriteLine("  The installed modules demand no environment values.");
                return 0;
            }

            var missing = 0;
            var width = requirements.Max(requirement => requirement.Key.Length);

            foreach (var requirement in requirements)
            {
                var present = target.TryGetValue(requirement.Key, out var value) && !string.IsNullOrWhiteSpace(value);

                if (!present)
                    missing++;

                Console.WriteLine($"  {(present ? "ok     " : "MISSING")}  {requirement.Key.PadRight(width)}  {requirement.Reason}");
            }

            var warnings = DeployRequirements.Warnings(project.Manifest, project.Root);

            if (warnings.Count > 0)
            {
                Console.WriteLine();

                foreach (var warning in warnings)
                    Console.WriteLine($"  note: {warning}");
            }

            Console.WriteLine();

            if (missing > 0)
            {
                Console.WriteLine($"{missing} required value(s) missing. Deploying now would crashloop at boot.");
                return 1;
            }

            Console.WriteLine("Every value the modules demand is present.");
            return 0;
        }

        /// <summary>
        /// Reads KEY=VALUE lines; comments and blanks are skipped, and both
        /// KEY=VALUE and export KEY=VALUE forms work.
        /// </summary>
        private static Dictionary<string, string> ParseEnvFile(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                if (line.StartsWith("export ", StringComparison.Ordinal))
                    line = line["export ".Length..];

                var separator = line.IndexOf('=');

                if (separator <= 0)
                    continue;

                values[line[..separator].Trim()] = line[(separator + 1)..].Trim().Trim('"');
            }

            return values;
        }
    }
}
