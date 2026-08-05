using System.Text;

namespace Truss.Cli
{
    internal static class ComposeGenerator
    {
        public static void Write(TrussManifest manifest, string rootDirectory)
        {
            if (!manifest.Docker)
                return;

            var services = new StringBuilder();

            switch (manifest.Database)
            {
                case "postgres":
                    services.AppendLine($"""
                          db:
                            image: postgres:16-alpine
                            environment:
                              POSTGRES_PASSWORD: truss
                              POSTGRES_DB: {manifest.Name.ToLowerInvariant()}
                            ports:
                              - "5432:5432"
                            volumes:
                              - db-data:/var/lib/postgresql/data
                        """);
                    break;

                case "sqlserver":
                    services.AppendLine("""
                          db:
                            image: mcr.microsoft.com/mssql/server:2022-latest
                            environment:
                              ACCEPT_EULA: "Y"
                              MSSQL_SA_PASSWORD: "Truss!Passw0rd"
                            ports:
                              - "1433:1433"
                            volumes:
                              - db-data:/var/opt/mssql
                        """);
                    break;
            }

            if (manifest.Settings.TryGetValue("messaging.transport", out var transport) && transport == "redis")
            {
                services.AppendLine("""
                      redis:
                        image: redis:7-alpine
                        ports:
                          - "6379:6379"
                    """);
            }

            if (services.Length == 0)
                return;

            var volumes = manifest.Database is "postgres" or "sqlserver"
                ? $"{Environment.NewLine}volumes:{Environment.NewLine}  db-data:{Environment.NewLine}"
                : Environment.NewLine;

            var compose = $"services:{Environment.NewLine}{services}{volumes}";

            File.WriteAllText(Path.Combine(rootDirectory, "docker-compose.yml"), compose);
        }
    }
}
