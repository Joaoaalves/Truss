using System.Text.RegularExpressions;

namespace Truss.Cli
{
    /// <summary>
    /// Removes a bounded context: deletes its folders across the layer projects
    /// and cleans every line that referenced its types from the composition
    /// roots, the DbContext and the infrastructure module. The type names are
    /// taken from the deleted file names, so generated slices unwind completely.
    /// </summary>
    internal static class ContextRemover
    {
        public static int Remove(TrussManifest manifest, string root, string context, Action<string> log)
        {
            if (!Naming.IsValidTypeName(context))
            {
                log($"'{context}' is not a valid context name.");
                return 1;
            }

            if (string.Equals(context, "Accounts", StringComparison.OrdinalIgnoreCase) && manifest.Modules.Contains("auth"))
            {
                log("The Accounts context belongs to the auth module; it is your code to edit, but removing it would leave the module half-wired, so this command refuses.");
                return 1;
            }

            var folders = new[]
                {
                    Path.Combine(root, manifest.DomainProject, context),
                    Path.Combine(root, manifest.ApplicationProject, context),
                    Path.Combine(root, manifest.InfrastructureProject, context),
                    Path.Combine(root, manifest.DomainTestsProject, context),
                    Path.Combine(root, manifest.IntegrationTestsProject, context)
                }
                .Where(Directory.Exists)
                .ToArray();

            if (folders.Length == 0)
            {
                log($"No {context} context was found in the project.");
                return 1;
            }

            var types = folders
                .SelectMany(folder => Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal);

            foreach (var folder in folders)
            {
                Directory.Delete(folder, recursive: true);
                log($"Removed {Path.GetRelativePath(root, folder)}");
            }

            CleanFile(Path.Combine(root, manifest.ApiProject, "Program.cs"), context, types, root, log);
            CleanFile(Path.Combine(root, "src", $"{manifest.Name}.Worker", "Program.cs"), context, types, root, log);
            CleanFile(Path.Combine(root, manifest.InfrastructureProject, "AppDbContext.cs"), context, types, root, log);
            CleanFile(Path.Combine(root, manifest.InfrastructureProject, "InfrastructureModule.cs"), context, types, root, log);

            RemoveEmptyInfrastructureModule(manifest, root, log);

            if (manifest.Sample && context == "Catalog")
            {
                manifest.Sample = false;
                manifest.Save(root);
            }

            if (Directory.Exists(Path.Combine(root, manifest.InfrastructureProject, "Migrations")))
                log($"The database schema still carries the removed tables; capture the drop with: truss db add Remove{context}");

            log("Run dotnet build; anything else that referenced the removed types will surface there.");
            return 0;
        }

        private static void CleanFile(string path, string context, HashSet<string> types, string root, Action<string> log)
        {
            if (!File.Exists(path))
                return;

            var contextUsing = new Regex($@"^\s*using\s+[\w.]+\.{Regex.Escape(context)};\s*$");
            var lines = File.ReadAllLines(path);
            var kept = new List<string>(lines.Length);
            var removed = 0;

            foreach (var line in lines)
            {
                if (contextUsing.IsMatch(line) || ReferencesRemovedType(line, types))
                {
                    removed++;
                    continue;
                }

                if (line.Trim().Length == 0 && kept.Count > 0 && kept[^1].Trim().Length == 0)
                    continue;

                kept.Add(line);
            }

            if (removed == 0)
                return;

            File.WriteAllLines(path, kept);
            log($"Cleaned {removed} line(s) from {Path.GetRelativePath(root, path)}");
        }

        private static bool ReferencesRemovedType(string line, HashSet<string> types)
        {
            return Regex.Split(line, @"\W+").Any(types.Contains);
        }

        /// <summary>
        /// An infrastructure module that no longer registers anything is sample
        /// residue; drop it together with its call in Program.cs.
        /// </summary>
        private static void RemoveEmptyInfrastructureModule(TrussManifest manifest, string root, Action<string> log)
        {
            var modulePath = Path.Combine(root, manifest.InfrastructureProject, "InfrastructureModule.cs");

            if (!File.Exists(modulePath) || File.ReadAllText(modulePath).Contains("services.Add"))
                return;

            File.Delete(modulePath);
            log($"Removed {Path.GetRelativePath(root, modulePath)} (it no longer registered anything)");

            var programPath = Path.Combine(root, manifest.ApiProject, "Program.cs");

            if (!File.Exists(programPath))
                return;

            var lines = File.ReadAllLines(programPath)
                .Where(line => line.Trim() != "builder.Services.AddInfrastructure();")
                .ToList();

            for (var index = lines.Count - 1; index > 0; index--)
            {
                if (lines[index].Trim().Length == 0 && lines[index - 1].Trim().Length == 0)
                    lines.RemoveAt(index);
            }

            File.WriteAllLines(programPath, lines);
        }
    }
}
