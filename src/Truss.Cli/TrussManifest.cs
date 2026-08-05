using System.Text.Json;
using System.Text.Json.Serialization;

namespace Truss.Cli
{
    internal sealed class TrussManifest
    {
        public const string FileName = "truss.json";

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };

        public string Name { get; set; } = string.Empty;

        public string TrussVersion { get; set; } = string.Empty;

        public string Database { get; set; } = "none";

        public bool Docker { get; set; }

        public bool Sample { get; set; }

        public List<string> Modules { get; set; } = [];

        public Dictionary<string, string> Settings { get; set; } = [];

        public bool UsesEntityFramework => Database is "postgres" or "sqlserver" or "sqlite";

        public string DomainProject => Path.Combine("src", $"{Name}.Domain");

        public string ApplicationProject => Path.Combine("src", $"{Name}.Application");

        public string InfrastructureProject => Path.Combine("src", $"{Name}.Infrastructure");

        public string ApiProject => Path.Combine("src", $"{Name}.Api");

        public void Save(string rootDirectory)
        {
            File.WriteAllText(Path.Combine(rootDirectory, FileName), JsonSerializer.Serialize(this, Json) + Environment.NewLine);
        }

        public static TrussManifest? Load(string rootDirectory)
        {
            var path = Path.Combine(rootDirectory, FileName);

            if (!File.Exists(path))
                return null;

            return JsonSerializer.Deserialize<TrussManifest>(File.ReadAllText(path), Json);
        }

        public static (TrussManifest Manifest, string Root)? Locate(string startDirectory)
        {
            var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));

            while (directory is not null)
            {
                var manifest = Load(directory.FullName);

                if (manifest is not null)
                    return (manifest, directory.FullName);

                directory = directory.Parent!;
            }

            return null;
        }
    }
}
