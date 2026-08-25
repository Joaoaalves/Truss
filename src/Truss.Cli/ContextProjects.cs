using Truss.Cli.Templates;

namespace Truss.Cli
{
    /// <summary>
    /// Turns a bounded context into its own set of projects, the first step of
    /// the extraction path. Namespaces do not change: the context keeps
    /// {App}.Domain.{Context} and friends, so moving an existing context is a
    /// file move and everything that referenced it compiles untouched. Three
    /// projects keep the layering compiler-enforced, and the context's
    /// repositories depend on DbContext instead of AppDbContext, so the context
    /// is one hosting decision away from owning its own database.
    /// </summary>
    internal static class ContextProjects
    {
        /// <summary>
        /// Whether the context lives in its own projects. The filesystem is the
        /// truth: no manifest state to keep in sync.
        /// </summary>
        public static bool Exists(TrussManifest manifest, string root, string context)
        {
            return Directory.Exists(ProjectDirectory(manifest, root, context, "Domain"));
        }

        /// <summary>
        /// Resolves where a generator should write for the given main-layer
        /// project, or null when the context is folder-based or the project has
        /// no per-context counterpart (the test projects stay shared).
        /// </summary>
        public static string? LayerDirectory(TrussManifest manifest, string root, string project, string context)
        {
            var layer = LayerOf(manifest, project);

            if (layer is null || !Exists(manifest, root, context))
                return null;

            return ProjectDirectory(manifest, root, context, layer);
        }

        public static IReadOnlyList<string> Create(TrussManifest manifest, string root, string context, Action<string> log)
        {
            if (string.Equals(context, "Accounts", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The Accounts context belongs to the auth module and stays in the main projects.");

            if (Exists(manifest, root, context))
            {
                log($"The {context} context already lives in its own projects.");
                return [];
            }

            // The Accounts scaffold references the bound aggregate's types, so
            // extracting its context would make the main projects depend on the
            // context's projects while the context depends back on the shared
            // domain. That coupling is real: it has to be undone before the
            // context can leave.
            if (manifest.Settings.TryGetValue("auth.bind", out var bound))
            {
                var contextDomain = Path.Combine(root, manifest.DomainProject, context);

                if (Directory.Exists(contextDomain)
                    && Directory.EnumerateFiles(contextDomain, $"{bound}.cs", SearchOption.AllDirectories).Any())
                {
                    throw new ArgumentException(
                        $"The account User is bound to the {bound} aggregate, so the {context} context cannot move into its own projects while the binding exists.");
                }
            }

            var moving = Directory.Exists(Path.Combine(root, manifest.DomainProject, context))
                || Directory.Exists(Path.Combine(root, manifest.ApplicationProject, context));

            WriteProjects(manifest, root, context);

            if (moving)
                MoveContext(manifest, root, context, log);

            Wire(manifest, root, context, log);

            log(moving
                ? $"The {context} context moved into its own projects. Namespaces did not change, so the rest of the code compiles as it was."
                : $"The {context} context was created in its own projects. Generators target them automatically: truss g agg <Name> -c {context}.");

            var layers = manifest.UsesEntityFramework ? Layers : LayersWithoutInfrastructure;
            return layers.Select(layer => ProjectDirectory(manifest, root, context, layer)).ToArray();
        }

        private static void WriteProjects(TrussManifest manifest, string root, string context)
        {
            Write(manifest, root, context, "Domain", $"{manifest.Name}.{context}.Domain.csproj",
                Render(ProjectTemplates.ContextDomainCsproj, manifest, context));

            Write(manifest, root, context, "Application", $"{manifest.Name}.{context}.Application.csproj",
                Render(ProjectTemplates.ContextApplicationCsproj, manifest, context)
                    .Replace("__MODULE_PACKAGES__", ModulePackages(manifest)));

            Write(manifest, root, context, "Application", $"{context}AssemblyMarker.cs",
                Render(ProjectTemplates.ContextAssemblyMarker, manifest, context));

            if (!manifest.UsesEntityFramework)
                return;

            Write(manifest, root, context, "Infrastructure", $"{manifest.Name}.{context}.Infrastructure.csproj",
                Render(ProjectTemplates.ContextInfrastructureCsproj, manifest, context));

            Write(manifest, root, context, "Infrastructure", $"{context}InfrastructureMarker.cs",
                Render(ProjectTemplates.ContextInfrastructureMarker, manifest, context));
        }

        /// <summary>
        /// Lifts the existing folders into the projects. Files move as they
        /// are; only infrastructure trades AppDbContext for DbContext, because
        /// the context's projects no longer see the main Infrastructure.
        /// </summary>
        private static void MoveContext(TrussManifest manifest, string root, string context, Action<string> log)
        {
            MoveContents(
                Path.Combine(root, manifest.DomainProject, context),
                ProjectDirectory(manifest, root, context, "Domain"));

            MoveContents(
                Path.Combine(root, manifest.ApplicationProject, context),
                ProjectDirectory(manifest, root, context, "Application"));

            if (!manifest.UsesEntityFramework)
                return;

            var infrastructure = ProjectDirectory(manifest, root, context, "Infrastructure");
            var moved = MoveContents(Path.Combine(root, manifest.InfrastructureProject, context), infrastructure);
            var rewritten = false;

            foreach (var file in moved.Where(file => file.EndsWith(".cs", StringComparison.Ordinal)))
            {
                var content = File.ReadAllText(file);

                if (!content.Contains("AppDbContext"))
                    continue;

                File.WriteAllText(file, content.Replace("AppDbContext", "DbContext"));
                rewritten = true;
            }

            if (rewritten)
                log("Repositories now receive DbContext; anything that used a DbSet property of AppDbContext must go through Set<T>().");

            PatchTestHosts(manifest, root, context);
        }

        /// <summary>
        /// The context's integration tests stay in the shared test project, but
        /// their hosts must now register the context's assembly to find its
        /// handlers.
        /// </summary>
        private static void PatchTestHosts(TrussManifest manifest, string root, string context)
        {
            var tests = Path.Combine(root, manifest.IntegrationTestsProject, context);

            if (!manifest.Tests || !Directory.Exists(tests))
                return;

            foreach (var file in Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories))
                File.WriteAllText(file, RegisterContextAssembly(File.ReadAllText(file), manifest, context));
        }

        private static void Wire(TrussManifest manifest, string root, string context, Action<string> log)
        {
            WireSolution(manifest, root, context, log);
            WireReferences(manifest, root, context);
            WireHost(Path.Combine(root, manifest.ApiProject, "Program.cs"), manifest, context, log);
            WireHost(Path.Combine(root, "src", $"{manifest.Name}.Worker", "Program.cs"), manifest, context, log);
            WireModel(manifest, root, context, log);
        }


        private static void WireSolution(TrussManifest manifest, string root, string context, Action<string> log)
        {
            var solution = Path.Combine(root, $"{manifest.Name}.slnx");
            var layers = manifest.UsesEntityFramework ? Layers : LayersWithoutInfrastructure;

            var entries = string.Join(Environment.NewLine, layers.Select(layer =>
                $"""    <Project Path="src/{manifest.Name}.{context}.{layer}/{manifest.Name}.{context}.{layer}.csproj" />"""));

            if (!SourceEditor.InsertBefore(solution, "  </Folder>", entries))
                log($"Could not update the solution automatically. Add to {manifest.Name}.slnx:{Environment.NewLine}{entries}");
        }

        private static void WireReferences(TrussManifest manifest, string root, string context)
        {
            if (manifest.UsesEntityFramework)
            {
                // The main Infrastructure hosts AppDbContext, which applies the
                // context's configurations, so it references the context's
                // infrastructure; the API sees everything through it.
                CsprojEditor.AddProjectReference(
                    Csproj(root, manifest.InfrastructureProject),
                    $"..\\{manifest.Name}.{context}.Infrastructure\\{manifest.Name}.{context}.Infrastructure.csproj");
            }
            else
            {
                CsprojEditor.AddProjectReference(
                    Csproj(root, manifest.ApiProject),
                    $"..\\{manifest.Name}.{context}.Application\\{manifest.Name}.{context}.Application.csproj");

                var worker = Path.Combine(root, "src", $"{manifest.Name}.Worker", $"{manifest.Name}.Worker.csproj");

                if (File.Exists(worker))
                {
                    CsprojEditor.AddProjectReference(
                        worker,
                        $"..\\{manifest.Name}.{context}.Application\\{manifest.Name}.{context}.Application.csproj");
                }
            }

            if (!manifest.Tests)
                return;

            CsprojEditor.AddProjectReference(
                Csproj(root, manifest.DomainTestsProject),
                $"..\\..\\src\\{manifest.Name}.{context}.Domain\\{manifest.Name}.{context}.Domain.csproj");

            CsprojEditor.AddProjectReference(
                Csproj(root, manifest.IntegrationTestsProject),
                $"..\\..\\src\\{manifest.Name}.{context}.Application\\{manifest.Name}.{context}.Application.csproj");

            if (manifest.UsesEntityFramework)
            {
                CsprojEditor.AddProjectReference(
                    Csproj(root, manifest.IntegrationTestsProject),
                    $"..\\..\\src\\{manifest.Name}.{context}.Infrastructure\\{manifest.Name}.{context}.Infrastructure.csproj");
            }
        }

        /// <summary>
        /// Registers the context's assembly beside the main application marker
        /// in a generated test host, mirroring what the composition root does.
        /// Used when generating into a project context and when moving one, so
        /// its existing tests keep resolving their handlers.
        /// </summary>
        public static string RegisterContextAssembly(string content, TrussManifest manifest, string context)
        {
            const string anchor = "options.AddAssembly<ApplicationAssemblyMarker>();";
            var marker = $"options.AddAssembly<{context}AssemblyMarker>();";

            if (content.Contains(marker) || !content.Contains(anchor))
                return content;

            var lines = content.Split('\n').ToList();
            var index = lines.FindIndex(line => line.TrimEnd().EndsWith(anchor, StringComparison.Ordinal));
            var indent = lines[index][..(lines[index].Length - lines[index].TrimStart().Length)];

            lines.Insert(index + 1, $"{indent}{marker}");

            var usingLine = $"using {manifest.Name}.Application.{context};";
            var usingIndex = lines.FindIndex(line => line.TrimEnd() == $"using {manifest.Name}.Application;");

            if (usingIndex >= 0 && !lines.Any(line => line.TrimEnd() == usingLine))
                lines.Insert(usingIndex + 1, usingLine);

            return string.Join('\n', lines);
        }

        /// <summary>
        /// Every context that lives in its own projects, read from the
        /// filesystem so there is no manifest state to drift.
        /// </summary>
        public static IEnumerable<string> All(TrussManifest manifest, string root)
        {
            var source = Path.Combine(root, "src");

            if (!Directory.Exists(source))
                yield break;

            var prefix = $"{manifest.Name}.";

            foreach (var directory in Directory.EnumerateDirectories(source, $"{prefix}*.Domain"))
            {
                var name = Path.GetFileName(directory);
                yield return name[prefix.Length..^".Domain".Length];
            }
        }

        /// <summary>
        /// Registers every project context's assembly beside each registration
        /// of the main application assembly, in the API and the worker.
        /// Idempotent and rerun after installs, so a messaging or jobs block
        /// added later picks the contexts up too.
        /// </summary>
        public static void WireHosts(TrussManifest manifest, string root, Action<string> log)
        {
            foreach (var context in All(manifest, root))
            {
                WireHost(Path.Combine(root, manifest.ApiProject, "Program.cs"), manifest, context, log);
                WireHost(Path.Combine(root, "src", $"{manifest.Name}.Worker", "Program.cs"), manifest, context, log);
            }
        }

        /// <summary>
        /// Registers the context's assembly wherever the host registers the main
        /// application assembly, so requests, events and jobs of the context are
        /// discovered exactly like before the move. Existing context lines are
        /// rebuilt, which makes the operation idempotent per registration block.
        /// </summary>
        private static void WireHost(string program, TrussManifest manifest, string context, Action<string> log)
        {
            if (!File.Exists(program))
                return;

            var anchor = "    options.AddAssembly<ApplicationAssemblyMarker>();";
            var marker = $"    options.AddAssembly<{context}AssemblyMarker>();";

            var lines = File.ReadAllLines(program)
                .Where(line => line.TrimEnd() != marker.TrimEnd())
                .ToList();

            if (!lines.Any(line => line.TrimEnd() == anchor.TrimEnd()))
            {
                log($"Could not update {Path.GetFileName(program)} automatically. Add options.AddAssembly<{context}AssemblyMarker>() beside every AddAssembly<ApplicationAssemblyMarker>().");
                return;
            }

            var updated = new List<string>();

            foreach (var line in lines)
            {
                updated.Add(line);

                if (line.TrimEnd() == anchor.TrimEnd())
                    updated.Add(marker);
            }

            File.WriteAllLines(program, updated);

            SourceEditor.InsertAfter(program, $"using {manifest.Name}.Application;", $"using {manifest.Name}.Application.{context};");
        }

        private static void WireModel(TrussManifest manifest, string root, string context, Action<string> log)
        {
            if (!manifest.UsesEntityFramework)
                return;

            var contextPath = Path.Combine(root, manifest.InfrastructureProject, "AppDbContext.cs");
            var line = $"            modelBuilder.ApplyConfigurationsFromAssembly(typeof({context}InfrastructureMarker).Assembly);";

            if (!SourceEditor.InsertAtMarker(contextPath, Markers.Model, line))
            {
                log($"Could not find the {Markers.Model} marker in AppDbContext.cs. Add to OnModelCreating: {line.Trim()}");
                return;
            }

            SourceEditor.InsertBefore(contextPath, "using Microsoft.EntityFrameworkCore;", $"using {manifest.Name}.Infrastructure.{context};");
        }

        private static string ModulePackages(TrussManifest manifest)
        {
            var packages = new List<string>();

            if (manifest.Modules.Contains("messaging"))
                packages.Add($"""    <PackageReference Include="Truss.Messaging" Version="{manifest.TrussVersion}" />""");

            if (manifest.Modules.Contains("jobs"))
                packages.Add($"""    <PackageReference Include="Truss.Jobs" Version="{manifest.TrussVersion}" />""");

            if (manifest.Modules.Contains("email"))
                packages.Add($"""    <PackageReference Include="Truss.Email" Version="{manifest.TrussVersion}" />""");

            return packages.Count == 0
                ? string.Empty
                : Environment.NewLine + string.Join(Environment.NewLine, packages);
        }

        private static IReadOnlyList<string> MoveContents(string source, string target)
        {
            var moved = new List<string>();

            if (!Directory.Exists(source))
                return moved;

            foreach (var entry in Directory.GetFileSystemEntries(source))
            {
                var destination = Path.Combine(target, Path.GetFileName(entry));

                if (Directory.Exists(entry))
                {
                    Directory.Move(entry, destination);
                    moved.AddRange(Directory.GetFiles(destination, "*", SearchOption.AllDirectories));
                }
                else
                {
                    File.Move(entry, destination);
                    moved.Add(destination);
                }
            }

            Directory.Delete(source);
            return moved;
        }

        private static readonly string[] Layers = ["Domain", "Application", "Infrastructure"];
        private static readonly string[] LayersWithoutInfrastructure = ["Domain", "Application"];

        private static string? LayerOf(TrussManifest manifest, string project)
        {
            return project == manifest.DomainProject ? "Domain"
                : project == manifest.ApplicationProject ? "Application"
                : project == manifest.InfrastructureProject ? "Infrastructure"
                : null;
        }

        private static string ProjectDirectory(TrussManifest manifest, string root, string context, string layer)
        {
            return Path.Combine(root, "src", $"{manifest.Name}.{context}.{layer}");
        }

        private static void Write(TrussManifest manifest, string root, string context, string layer, string fileName, string content)
        {
            var directory = ProjectDirectory(manifest, root, context, layer);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, fileName), content + Environment.NewLine);
        }

        private static string Render(string template, TrussManifest manifest, string context)
        {
            return template
                .Replace("__TRUSS_VERSION__", manifest.TrussVersion)
                .Replace("__CONTEXT__", context)
                .Replace("__NAME__", manifest.Name);
        }

        private static string Csproj(string root, string projectDirectory)
        {
            return Directory.EnumerateFiles(Path.Combine(root, projectDirectory), "*.csproj").First();
        }
    }
}
