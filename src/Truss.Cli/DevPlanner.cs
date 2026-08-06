using System.Text.Json.Nodes;

namespace Truss.Cli
{
    internal sealed record DevUrl(string Label, string Url);

    internal sealed record DevPlan(bool RunCompose, string ApiProjectPath, IReadOnlyList<DevUrl> Urls);

    /// <summary>
    /// Computes what truss dev should do for a project: whether compose runs,
    /// which project to watch and which URLs matter during development.
    /// </summary>
    internal static class DevPlanner
    {
        public static DevPlan Build(TrussManifest manifest, string root)
        {
            var urls = new List<DevUrl>();
            var applicationUrl = ReadApplicationUrl(manifest, root) ?? "http://localhost:5000";

            urls.Add(new DevUrl("API", applicationUrl));

            if (HasOpenApi(manifest, root))
                urls.Add(new DevUrl("Scalar", $"{applicationUrl.TrimEnd('/')}/scalar"));

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

            var runCompose = manifest.Docker && File.Exists(Path.Combine(root, "docker-compose.yml"));

            return new DevPlan(runCompose, Path.Combine(root, manifest.ApiProject), urls);
        }

        private static string? ReadApplicationUrl(TrussManifest manifest, string root)
        {
            var path = Path.Combine(root, manifest.ApiProject, "Properties", "launchSettings.json");

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

        private static bool HasOpenApi(TrussManifest manifest, string root)
        {
            var program = Path.Combine(root, manifest.ApiProject, "Program.cs");
            return File.Exists(program) && File.ReadAllText(program).Contains("MapScalarApiReference");
        }
    }
}
