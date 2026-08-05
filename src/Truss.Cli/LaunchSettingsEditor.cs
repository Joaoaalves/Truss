using System.Text.Json;
using System.Text.Json.Nodes;

namespace Truss.Cli
{
    internal static class LaunchSettingsEditor
    {
        public static void SetEnvironmentVariables(string root, TrussManifest manifest, Dictionary<string, string> variables, Action<string> log)
        {
            var path = Path.Combine(root, manifest.ApiProject, "Properties", "launchSettings.json");

            if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject settings
                || settings["profiles"] is not JsonObject profiles)
            {
                log("Could not update launchSettings.json; set these environment variables yourself:");

                foreach (var pair in variables)
                    log($"  {pair.Key}={pair.Value}");

                return;
            }

            foreach (var profile in profiles)
            {
                if (profile.Value is not JsonObject profileObject)
                    continue;

                if (profileObject["environmentVariables"] is not JsonObject environment)
                {
                    environment = new JsonObject();
                    profileObject["environmentVariables"] = environment;
                }

                foreach (var pair in variables)
                    environment[pair.Key] = pair.Value;
            }

            File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        }
    }
}
