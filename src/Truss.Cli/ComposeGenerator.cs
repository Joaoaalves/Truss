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

            manifest.Settings.TryGetValue("messaging.transport", out var transport);

            if (transport == "redis")
            {
                services.AppendLine("""
                      redis:
                        image: redis:7-alpine
                        ports:
                          - "6379:6379"
                    """);
            }
            else if (transport == "rabbitmq")
            {
                services.AppendLine("""
                      rabbitmq:
                        image: rabbitmq:4-management-alpine
                        ports:
                          - "5672:5672"
                          - "15672:15672"
                        volumes:
                          - rabbitmq-data:/var/lib/rabbitmq
                    """);
            }

            manifest.Settings.TryGetValue("observability.dashboard", out var dashboard);

            switch (dashboard)
            {
                case "aspire":
                    services.AppendLine("""
                          dashboard:
                            image: mcr.microsoft.com/dotnet/aspire-dashboard:9.5
                            environment:
                              DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS: "true"
                            ports:
                              - "18888:18888"
                              - "4317:18889"
                        """);
                    break;

                case "grafana":
                    services.AppendLine("""
                          dashboard:
                            image: grafana/otel-lgtm:latest
                            ports:
                              - "3000:3000"
                              - "4317:4317"
                        """);
                    break;

                case "seq":
                    services.AppendLine("""
                          seq:
                            image: datalust/seq:latest
                            environment:
                              ACCEPT_EULA: "Y"
                            ports:
                              - "5341:5341"
                              - "8081:80"
                            volumes:
                              - seq-data:/data
                        """);
                    break;
            }

            if (services.Length == 0)
                return;

            var volumeNames = new List<string>();

            if (manifest.Database is "postgres" or "sqlserver")
                volumeNames.Add("db-data");

            if (transport == "rabbitmq")
                volumeNames.Add("rabbitmq-data");

            if (dashboard == "seq")
                volumeNames.Add("seq-data");

            var volumes = volumeNames.Count > 0
                ? $"{Environment.NewLine}volumes:{Environment.NewLine}{string.Join(Environment.NewLine, volumeNames.Select(name => $"  {name}:"))}{Environment.NewLine}"
                : Environment.NewLine;

            var compose = $"services:{Environment.NewLine}{services}{volumes}";

            File.WriteAllText(Path.Combine(rootDirectory, "docker-compose.yml"), compose);
        }
    }
}
