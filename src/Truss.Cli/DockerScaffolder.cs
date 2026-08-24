namespace Truss.Cli
{
    /// <summary>
    /// Writes the production images of the application: one Dockerfile per
    /// host, multi-stage, alpine-based, running as the non-root user the .NET
    /// images ship, with a real healthcheck against /health. The compose file
    /// keeps serving development dependencies; these images are what a
    /// registry, a VPS or a cluster runs.
    /// </summary>
    internal static class DockerScaffolder
    {
        public static int Install(TrussManifest manifest, string root, Action<string> log)
        {
            WriteDockerignore(root);
            WriteAll(manifest, root, log);

            log("Build an image from the repository root: docker build -f src/<Host>/Dockerfile -t <tag> .");
            log("Each host binds to 8080 inside the container; the orchestrator maps it and /health answers the probes.");

            return 0;
        }

        /// <summary>
        /// Writes the Dockerfile of a host that appeared after the module was
        /// installed (a split service, the worker), so each of them ships with
        /// its image from birth.
        /// </summary>
        public static void WriteHostDockerfiles(TrussManifest manifest, string root, Action<string> log)
        {
            if (manifest.Modules.Contains("docker"))
                WriteAll(manifest, root, log);
        }

        private static void WriteAll(TrussManifest manifest, string root, Action<string> log)
        {
            WriteApi(manifest, root, manifest.ApiProject, log);

            foreach (var service in Directory.EnumerateDirectories(Path.Combine(root, "src"), $"{manifest.Name}.*.Api"))
                WriteApi(manifest, root, Path.GetRelativePath(root, service), log);

            var worker = Path.Combine("src", $"{manifest.Name}.Worker");

            if (Directory.Exists(Path.Combine(root, worker)))
                WriteWorker(manifest, root, worker, log);
        }

        private static void WriteApi(TrussManifest manifest, string root, string project, Action<string> log)
        {
            var name = Path.GetFileName(project.TrimEnd(Path.DirectorySeparatorChar, '/'));

            Write(root, project, ApiDockerfile
                .Replace("__PROJECT__", $"src/{name}/{name}.csproj")
                .Replace("__DLL__", name), log);
        }

        private static void WriteWorker(TrussManifest manifest, string root, string project, Action<string> log)
        {
            var name = Path.GetFileName(project);

            Write(root, project, WorkerDockerfile
                .Replace("__PROJECT__", $"src/{name}/{name}.csproj")
                .Replace("__DLL__", name), log);
        }

        private static void Write(string root, string project, string content, Action<string> log)
        {
            var path = Path.Combine(root, project, "Dockerfile");

            if (File.Exists(path))
                return;

            File.WriteAllText(path, content + Environment.NewLine);
            log($"created {Path.GetRelativePath(root, path)}");
        }

        private static void WriteDockerignore(string root)
        {
            var path = Path.Combine(root, ".dockerignore");

            if (!File.Exists(path))
                File.WriteAllText(path, Dockerignore + Environment.NewLine);
        }

        // The whole source tree is the build context, so project references
        // resolve; the publish of one host prunes everything it does not use.
        // Alpine keeps the image small, icu restores real globalization, and
        // APP_UID is the non-root user the .NET images define.
        private const string ApiDockerfile = """
            FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
            WORKDIR /source

            COPY . .
            RUN dotnet publish __PROJECT__ -c Release -o /app

            FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
            RUN apk add --no-cache icu-libs tzdata
            ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

            WORKDIR /app
            COPY --from=build /app .

            ENV ASPNETCORE_URLS=http://0.0.0.0:8080
            EXPOSE 8080
            USER $APP_UID

            HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
                CMD wget -qO- http://127.0.0.1:8080/health || exit 1

            ENTRYPOINT ["dotnet", "__DLL__.dll"]
            """;

        private const string WorkerDockerfile = """
            FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
            WORKDIR /source

            COPY . .
            RUN dotnet publish __PROJECT__ -c Release -o /app

            FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
            RUN apk add --no-cache icu-libs tzdata
            ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

            WORKDIR /app
            COPY --from=build /app .

            USER $APP_UID

            ENTRYPOINT ["dotnet", "__DLL__.dll"]
            """;

        private const string Dockerignore = """
            **/bin
            **/obj
            **/*.db
            .git
            .claude
            docs
            """;
    }
}
