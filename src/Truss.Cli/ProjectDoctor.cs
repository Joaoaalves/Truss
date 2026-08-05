namespace Truss.Cli
{
    internal static class ProjectDoctor
    {
        public static int Run(TrussManifest manifest, string root, Action<string> log)
        {
            var problems = 0;

            Check(File.Exists(Path.Combine(root, $"{manifest.Name}.slnx")), $"solution {manifest.Name}.slnx", log, ref problems);
            Check(ProjectExists(root, manifest.DomainProject), "domain project", log, ref problems);
            Check(ProjectExists(root, manifest.ApplicationProject), "application project", log, ref problems);
            Check(ProjectExists(root, manifest.ApiProject), "api project", log, ref problems);

            if (manifest.UsesEntityFramework)
            {
                Check(ProjectExists(root, manifest.InfrastructureProject), "infrastructure project", log, ref problems);
                Check(File.Exists(Path.Combine(root, manifest.InfrastructureProject, "AppDbContext.cs")), "AppDbContext", log, ref problems);
            }

            if (manifest.Docker)
                Check(File.Exists(Path.Combine(root, "docker-compose.yml")), "docker-compose.yml", log, ref problems);

            foreach (var module in manifest.Modules)
            {
                var package = module switch
                {
                    "messaging" => "Truss.Messaging",
                    "jobs" => "Truss.Jobs",
                    "mapping" => "Truss.Mapping",
                    _ => "Truss.Observability"
                };

                Check(AnyProjectReferences(root, manifest, package), $"{module} module packages", log, ref problems);
            }

            if (problems == 0)
            {
                log("Everything looks consistent.");
                return 0;
            }

            log($"{problems} problem(s) found.");
            return 1;
        }

        private static bool ProjectExists(string root, string projectDirectory)
        {
            var directory = Path.Combine(root, projectDirectory);
            return Directory.Exists(directory) && Directory.EnumerateFiles(directory, "*.csproj").Any();
        }

        private static bool AnyProjectReferences(string root, TrussManifest manifest, string packageId)
        {
            var projects = new[] { manifest.ApplicationProject, manifest.InfrastructureProject, manifest.ApiProject };

            return projects
                .Select(project => Path.Combine(root, project))
                .Where(Directory.Exists)
                .SelectMany(directory => Directory.EnumerateFiles(directory, "*.csproj"))
                .Any(csproj => File.ReadAllText(csproj).Contains($"Include=\"{packageId}\""));
        }

        private static void Check(bool ok, string subject, Action<string> log, ref int problems)
        {
            if (ok)
            {
                log($"ok      {subject}");
            }
            else
            {
                log($"MISSING {subject}");
                problems++;
            }
        }
    }
}
