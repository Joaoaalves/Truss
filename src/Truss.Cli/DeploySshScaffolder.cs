using System.Text;

namespace Truss.Cli
{
    /// <summary>
    /// Generates the artifacts of a compose-over-SSH deployment, the cheapest
    /// production story for a VPS: a production compose file with every host
    /// and its backing services, a deploy script that builds, pushes, migrates
    /// and rolls out with a one-command rollback, and an example env file
    /// derived from the same requirements deploy check verifies. The files are
    /// the user's to edit; the CLI never deploys anything itself.
    /// </summary>
    internal static class DeploySshScaffolder
    {
        public static int Install(TrussManifest manifest, string root, Action<string> log)
        {
            if (!manifest.Modules.Contains("docker"))
            {
                log("The ssh deployment ships the Docker images. Run first: truss add docker");
                return 1;
            }

            var directory = Path.Combine(root, "deploy");
            Directory.CreateDirectory(directory);

            WriteIfMissing(Path.Combine(directory, "compose.production.yml"), BuildCompose(manifest, root), log, root);
            WriteIfMissing(Path.Combine(directory, "deploy.sh"), BuildScript(manifest, root), log, root, executable: true);
            WriteIfMissing(Path.Combine(directory, ".env.production.example"), BuildEnvExample(manifest, root), log, root);

            log("Set REGISTRY and SERVER (environment or at the top of deploy/deploy.sh), keep the real .env on the server, and deploy with: ./deploy/deploy.sh");
            log("Roll back to the previous image with: ./deploy/deploy.sh rollback");

            return 0;
        }

        private static IReadOnlyList<(string Slug, string Project, bool Web)> Hosts(TrussManifest manifest, string root)
        {
            var hosts = new List<(string, string, bool)> { ("api", Path.GetFileName(manifest.ApiProject), true) };

            foreach (var service in Directory.EnumerateDirectories(Path.Combine(root, "src"), $"{manifest.Name}.*.Api").OrderBy(name => name, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(service);
                var slug = name[(manifest.Name.Length + 1)..^".Api".Length].ToLowerInvariant();
                hosts.Add((slug, name, true));
            }

            if (Directory.Exists(Path.Combine(root, "src", $"{manifest.Name}.Worker")))
                hosts.Add(("worker", $"{manifest.Name}.Worker", false));

            return hosts;
        }

        private static string BuildCompose(TrussManifest manifest, string root)
        {
            var app = manifest.Name.ToLowerInvariant();
            var compose = new StringBuilder();
            var port = 8080;
            var dependencies = new List<string>();

            if (manifest.Database == "postgres")
                dependencies.Add("postgres");

            manifest.Settings.TryGetValue("messaging.transport", out var transport);

            if (transport == "rabbitmq")
                dependencies.Add("rabbitmq");
            else if (transport == "redis")
                dependencies.Add("redis");

            compose.AppendLine("services:");

            foreach (var (slug, project, web) in Hosts(manifest, root))
            {
                compose.AppendLine($"  {slug}:");
                compose.AppendLine($"    image: ${{REGISTRY}}/{app}-{slug}:${{TAG}}");
                compose.AppendLine("    restart: unless-stopped");
                compose.AppendLine("    env_file: .env");

                if (web)
                {
                    compose.AppendLine("    ports:");
                    compose.AppendLine($"      - \"{port}:8080\"");
                    port++;
                }

                if (dependencies.Count > 0)
                {
                    compose.AppendLine("    depends_on:");

                    foreach (var dependency in dependencies)
                        compose.AppendLine($"      - {dependency}");
                }

                compose.AppendLine();
            }

            if (manifest.Database == "postgres")
            {
                compose.AppendLine($$"""
                      postgres:
                        image: postgres:16-alpine
                        restart: unless-stopped
                        environment:
                          POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
                          POSTGRES_DB: {{app}}
                        volumes:
                          - postgres-data:/var/lib/postgresql/data

                    """);
            }

            if (transport == "rabbitmq")
            {
                compose.AppendLine("""
                      rabbitmq:
                        image: rabbitmq:4-alpine
                        restart: unless-stopped
                        volumes:
                          - rabbitmq-data:/var/lib/rabbitmq

                    """);
            }
            else if (transport == "redis")
            {
                compose.AppendLine("""
                      redis:
                        image: redis:7-alpine
                        restart: unless-stopped
                        volumes:
                          - redis-data:/data

                    """);
            }

            var volumes = new List<string>();

            if (manifest.Database == "postgres")
                volumes.Add("postgres-data");

            if (transport == "rabbitmq")
                volumes.Add("rabbitmq-data");
            else if (transport == "redis")
                volumes.Add("redis-data");

            if (volumes.Count > 0)
            {
                compose.AppendLine("volumes:");

                foreach (var volume in volumes)
                    compose.AppendLine($"  {volume}:");
            }

            return compose.ToString().TrimEnd() + Environment.NewLine;
        }

        private static string BuildScript(TrussManifest manifest, string root)
        {
            var app = manifest.Name.ToLowerInvariant();
            var builds = new StringBuilder();

            foreach (var (slug, project, _) in Hosts(manifest, root))
            {
                builds.AppendLine($"docker build -f \"src/{project}/Dockerfile\" -t \"$REGISTRY/{app}-{slug}:$TAG\" .");
                builds.AppendLine($"docker push \"$REGISTRY/{app}-{slug}:$TAG\"");
            }

            var migration = manifest.UsesEntityFramework
                ? $"""

                  # The migration travels as an EF bundle: a self-contained executable
                  # that runs on the server against its own database, before the new
                  # images start.
                  if ls "src/{Path.GetFileName(manifest.InfrastructureProject)}/Migrations"/*.cs >/dev/null 2>&1; then
                      dotnet ef migrations bundle \
                          --project "src/{Path.GetFileName(manifest.InfrastructureProject)}" \
                          --startup-project "src/{Path.GetFileName(manifest.ApiProject)}" \
                          --self-contained -r linux-x64 -o deploy/migrate --force
                      scp deploy/migrate "$SERVER:$APP_DIR/migrate"
                      ssh "$SERVER" "cd '$APP_DIR' && set -a && . ./.env && set +a && ./migrate --connection \"\$ConnectionStrings__Default\""
                  fi
                  """
                : string.Empty;

            return $$"""
                #!/usr/bin/env bash
                # Deploys {{manifest.Name}} to a single server over SSH with docker compose.
                # Generated by truss deploy init ssh; this file is yours to edit.
                #
                #   ./deploy/deploy.sh            builds, pushes, migrates and rolls out
                #   ./deploy/deploy.sh rollback   returns to the previously deployed tag
                #
                # The server needs docker with the compose plugin, and the real
                # environment lives ONLY there, in $APP_DIR/.env (start from
                # deploy/.env.production.example; truss deploy check validates it).
                set -euo pipefail
                cd "$(dirname "$0")/.."

                REGISTRY="${REGISTRY:?set REGISTRY, e.g. ghcr.io/you}"
                SERVER="${SERVER:?set SERVER, e.g. deploy@your-vps}"
                APP_DIR="${APP_DIR:-/opt/{{app}}}"
                TAG="${TAG:-$(git rev-parse --short HEAD)}"

                if [ "${1:-deploy}" = "rollback" ]; then
                    ssh "$SERVER" "cd '$APP_DIR' && TAG=\$(cat .previous-tag) docker compose -f compose.production.yml up -d"
                    echo "Rolled back to \$(ssh "$SERVER" cat "$APP_DIR/.previous-tag")."
                    exit 0
                fi

                # Refuse to ship into an environment that would crashloop at boot.
                ssh "$SERVER" "cat '$APP_DIR/.env'" > /tmp/{{app}}-deploy-env
                truss deploy check --env-file /tmp/{{app}}-deploy-env
                rm /tmp/{{app}}-deploy-env

                {{builds.ToString().TrimEnd()}}
                {{migration}}
                ssh "$SERVER" "mkdir -p '$APP_DIR'"
                scp deploy/compose.production.yml "$SERVER:$APP_DIR/compose.production.yml"

                ssh "$SERVER" "cd '$APP_DIR' \
                    && (grep -s '^TAG=' .deploy-tags | cut -d= -f2 > .previous-tag || true) \
                    && echo 'TAG=$TAG' > .deploy-tags \
                    && REGISTRY='$REGISTRY' TAG='$TAG' docker compose -f compose.production.yml pull \
                    && REGISTRY='$REGISTRY' TAG='$TAG' docker compose -f compose.production.yml up -d"

                echo "Deployed $TAG."
                """;
        }

        private static string BuildEnvExample(TrussManifest manifest, string root)
        {
            var example = new StringBuilder();

            example.AppendLine("# The environment the application demands at boot, verified by truss deploy check.");
            example.AppendLine("# Copy to the server as .env beside compose.production.yml; secrets never enter git.");
            example.AppendLine();
            example.AppendLine("REGISTRY=");
            example.AppendLine("TAG=");

            if (manifest.Database == "postgres")
                example.AppendLine("POSTGRES_PASSWORD=");

            foreach (var requirement in DeployRequirements.Build(manifest))
            {
                example.AppendLine();
                example.AppendLine($"# {requirement.Reason}");
                example.AppendLine($"{requirement.Key}=");
            }

            return example.ToString().TrimEnd() + Environment.NewLine;
        }

        private static void WriteIfMissing(string path, string content, Action<string> log, string root, bool executable = false)
        {
            if (File.Exists(path))
                return;

            File.WriteAllText(path, content);

            if (executable && !OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, File.GetUnixFileMode(path) | UnixFileMode.UserExecute | UnixFileMode.GroupExecute);

            log($"created {Path.GetRelativePath(root, path)}");
        }
    }
}
