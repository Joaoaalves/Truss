using System.Text.Json.Nodes;

namespace Truss.Cli
{
    internal sealed record DevUrl(string Label, string Url);

    internal sealed record DevHost(string Label, string ProjectPath);

    internal sealed record DevPlan(bool RunCompose, string ApiProjectPath, IReadOnlyList<DevUrl> Urls, IReadOnlyList<DevHost> Hosts);

    /// <summary>
    /// Computes what truss dev should do for a project: whether compose runs,
    /// which hosts make up the constellation and which URLs matter during
    /// development. With split services, every host runs; the trace of one
    /// request crosses all of them.
    /// </summary>
    internal static class DevPlanner
    {
        public static DevPlan Build(TrussManifest manifest, string root)
        {
            var urls = new List<DevUrl>();
            var hosts = new List<DevHost> { new("api", Path.Combine(root, manifest.ApiProject)) };
            var applicationUrl = ReadApplicationUrl(root, manifest.ApiProject) ?? "http://localhost:5000";

            urls.Add(new DevUrl("API", applicationUrl));

            var program = ReadProgram(manifest, root);

            if (program.Contains("MapScalarApiReference"))
                urls.Add(new DevUrl("Scalar", $"{applicationUrl.TrimEnd('/')}/scalar"));

            if (program.Contains("MapHealthChecks"))
                urls.Add(new DevUrl("Health", $"{applicationUrl.TrimEnd('/')}/health"));

            foreach (var directory in ServiceDirectories(manifest, root))
            {
                var label = ServiceLabel(manifest, directory);
                hosts.Add(new DevHost(label.ToLowerInvariant(), directory));

                if (ReadApplicationUrl(root, Path.GetRelativePath(root, directory)) is { } serviceUrl)
                    urls.Add(new DevUrl(label, serviceUrl));
            }

            var worker = Path.Combine(root, "src", $"{manifest.Name}.Worker");

            if (Directory.Exists(worker))
                hosts.Add(new DevHost("worker", worker));

            if (manifest.Modules.Contains("jobs"))
                urls.Add(new DevUrl("Jobs", $"{applicationUrl.TrimEnd('/')}/truss/jobs/{{id}}"));

            manifest.Settings.TryGetValue("observability.dashboard", out var dashboard);

            switch (dashboard)
            {
                case "aspire":
                    urls.Add(new DevUrl("Dashboard", "http://localhost:18888"));
                    break;
                case "grafana":
                    urls.Add(new DevUrl("Dashboard", "http://localhost:3000"));
                    break;
                case "seq":
                    urls.Add(new DevUrl("Dashboard", "http://localhost:8081"));
                    break;
            }

            if (manifest.Settings.TryGetValue("messaging.transport", out var transport) && transport == "rabbitmq")
                urls.Add(new DevUrl("RabbitMQ", "http://localhost:15672"));

            if (manifest.Settings.TryGetValue("email.provider", out var emailProvider) && emailProvider == "smtp")
                urls.Add(new DevUrl("Mailpit", "http://localhost:8025"));

            var runCompose = manifest.Docker && File.Exists(Path.Combine(root, "docker-compose.yml"));

            return new DevPlan(runCompose, Path.Combine(root, manifest.ApiProject), urls, hosts);
        }

        /// <summary>
        /// The split services, read from the filesystem like everything else:
        /// src/{App}.{Context}.Api directories.
        /// </summary>
        private static IEnumerable<string> ServiceDirectories(TrussManifest manifest, string root)
        {
            var source = Path.Combine(root, "src");

            if (!Directory.Exists(source))
                yield break;

            foreach (var directory in Directory.EnumerateDirectories(source, $"{manifest.Name}.*.Api").OrderBy(name => name, StringComparer.Ordinal))
                yield return directory;
        }

        private static string ServiceLabel(TrussManifest manifest, string directory)
        {
            var name = Path.GetFileName(directory);
            return name[(manifest.Name.Length + 1)..^".Api".Length];
        }

        private static string? ReadApplicationUrl(string root, string project)
        {
            var path = Path.Combine(root, project, "Properties", "launchSettings.json");

            if (!File.Exists(path))
                return null;

            try
            {
                var profiles = JsonNode.Parse(File.ReadAllText(path))?["profiles"]?.AsObject();

                foreach (var profile in profiles ?? [])
                {
                    if (profile.Value?["applicationUrl"]?.GetValue<string>() is { } url)
                        return url.Split(';')[0];
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }

            return null;
        }

        private static string ReadProgram(TrussManifest manifest, string root)
        {
            var program = Path.Combine(root, manifest.ApiProject, "Program.cs");
            return File.Exists(program) ? File.ReadAllText(program) : string.Empty;
        }
    }
}
