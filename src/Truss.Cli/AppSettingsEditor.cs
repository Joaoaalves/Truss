using System.Text.Json;
using System.Text.Json.Nodes;

namespace Truss.Cli
{
    internal static class AppSettingsEditor
    {
        public static void SetSection(string root, TrussManifest manifest, string colonPath, Dictionary<string, object> values, Action<string> log)
        {
            var path = Path.Combine(root, manifest.ApiProject, "appsettings.json");

            if (!File.Exists(path) || JsonNode.Parse(File.ReadAllText(path)) is not JsonObject settings)
            {
                log($"Could not update appsettings.json; add the {colonPath} section yourself.");
                return;
            }

            var section = settings;

            foreach (var segment in colonPath.Split(':'))
            {
                if (section[segment] is not JsonObject child)
                {
                    child = new JsonObject();
                    section[segment] = child;
                }

                section = child;
            }

            foreach (var pair in values)
            {
                section[pair.Key] = pair.Value switch
                {
                    string text => JsonValue.Create(text),
                    int number => JsonValue.Create(number),
                    bool flag => JsonValue.Create(flag),
                    _ => throw new ArgumentException($"Unsupported settings value type: {pair.Value.GetType()}.")
                };
            }

            File.WriteAllText(path, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
        }
    }
}
