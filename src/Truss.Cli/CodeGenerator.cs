using Truss.Cli.Templates;

namespace Truss.Cli
{
    internal static class CodeGenerator
    {
        public static IReadOnlyList<string> GenerateContext(TrussManifest manifest, string root, string context)
        {
            ValidateType(context);

            var domain = Path.Combine(root, manifest.DomainProject, context);
            var application = Path.Combine(root, manifest.ApplicationProject, context);

            Directory.CreateDirectory(domain);
            Directory.CreateDirectory(application);

            return [domain, application];
        }

        public static IReadOnlyList<string> GenerateAggregate(TrussManifest manifest, string root, string name, string? context, bool crud = false)
        {
            ValidateType(name);

            if (crud && !manifest.UsesEntityFramework)
                throw new ArgumentException("--crud generates a repository over the database and requires one. Scaffold the project with --database first.");

            var folder = Path.Combine(TargetDirectory(root, manifest.DomainProject, context), name);

            var files = new List<string>
            {
                WriteFile(folder, $"{name}.cs", RenderRich(crud ? GeneratorTemplates.AggregateCrud : GeneratorTemplates.Aggregate, manifest, name, context)),
                WriteFile(Path.Combine(folder, "ValueObjects"), $"{name}Id.cs", RenderRich(GeneratorTemplates.AggregateId, manifest, name, context)),
                WriteFile(Path.Combine(folder, "Events"), $"{name}Created.cs", RenderRich(GeneratorTemplates.AggregateCreated, manifest, name, context)),
                WriteFile(Path.Combine(folder, "Rules"), $"{name}MustBeValid.cs", RenderRich(GeneratorTemplates.AggregateRule, manifest, name, context))
            };

            if (HasTests(manifest, root, manifest.DomainTestsProject))
            {
                files.Add(WriteFile(
                    TargetDirectory(root, manifest.DomainTestsProject, context),
                    $"{name}Tests.cs",
                    RenderTest(crud ? TestTemplates.AggregateCrudTests : TestTemplates.AggregateTests, manifest, name, context)));
            }

            if (crud)
            {
                files.AddRange(GenerateCrud(manifest, root, name, context));

                if (HasTests(manifest, root, manifest.IntegrationTestsProject))
                {
                    files.Add(WriteFile(
                        TargetDirectory(root, manifest.IntegrationTestsProject, context),
                        $"{name}CrudTests.cs",
                        RenderTest(TestTemplates.CrudIntegrationTests, manifest, name, context)));
                }
            }

            return files;
        }

        public static IReadOnlyList<string> GenerateEntity(TrussManifest manifest, string root, string name, string? context, string? aggregate)
        {
            ValidateType(name);

            if (aggregate is not null)
                ValidateType(aggregate);

            var owner = aggregate ?? name;
            var folder = Path.Combine(TargetDirectory(root, manifest.DomainProject, context), owner);

            return
            [
                WriteFile(folder, $"{name}.cs", RenderRich(GeneratorTemplates.Entity, manifest, name, context, owner)),
                WriteFile(Path.Combine(folder, "ValueObjects"), $"{name}Id.cs", RenderRich(GeneratorTemplates.AggregateId, manifest, name, context, owner))
            ];
        }

        private static IEnumerable<string> GenerateCrud(TrussManifest manifest, string root, string name, string? context)
        {
            var feature = Path.Combine(TargetDirectory(root, manifest.ApplicationProject, context), name);
            var infrastructure = TargetDirectory(root, manifest.InfrastructureProject, context);

            yield return WriteFile(Path.Combine(feature, "DTOs"), $"{name}Dto.cs", RenderRich(GeneratorTemplates.CrudDto, manifest, name, context));
            yield return WriteFile(feature, $"I{name}Repository.cs", RenderRich(GeneratorTemplates.CrudRepository, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, "Rules"), $"{name}MustExist.cs", RenderRich(GeneratorTemplates.CrudMustExist, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Create{name}"), $"Create{name}.cs", RenderRich(GeneratorTemplates.CrudCreate, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Create{name}"), $"Create{name}Handler.cs", RenderRich(GeneratorTemplates.CrudCreateHandler, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Create{name}"), $"Create{name}Validator.cs", RenderRich(GeneratorTemplates.CrudCreateValidator, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Update{name}"), $"Update{name}.cs", RenderRich(GeneratorTemplates.CrudUpdate, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Update{name}"), $"Update{name}Handler.cs", RenderRich(GeneratorTemplates.CrudUpdateHandler, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Update{name}"), $"Update{name}Validator.cs", RenderRich(GeneratorTemplates.CrudUpdateValidator, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Delete{name}"), $"Delete{name}.cs", RenderRich(GeneratorTemplates.CrudDelete, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Delete{name}"), $"Delete{name}Handler.cs", RenderRich(GeneratorTemplates.CrudDeleteHandler, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Get{name}ById"), $"Get{name}ById.cs", RenderRich(GeneratorTemplates.CrudGetById, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Get{name}ById"), $"Get{name}ByIdHandler.cs", RenderRich(GeneratorTemplates.CrudGetByIdHandler, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"List{name}"), $"List{name}.cs", RenderRich(GeneratorTemplates.CrudList, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"List{name}"), $"List{name}Handler.cs", RenderRich(GeneratorTemplates.CrudListHandler, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"List{name}"), $"List{name}Validator.cs", RenderRich(GeneratorTemplates.CrudListValidator, manifest, name, context));
            yield return WriteFile(infrastructure, $"{name}Configuration.cs", RenderRich(GeneratorTemplates.CrudConfiguration, manifest, name, context));
            yield return WriteFile(infrastructure, $"Ef{name}Repository.cs", RenderRich(GeneratorTemplates.CrudEfRepository, manifest, name, context));
        }

        internal static void WireCrud(TrussManifest manifest, string root, string name, string? context, Action<string> log)
        {
            var program = Path.Combine(root, manifest.ApiProject, "Program.cs");
            var feature = $"{ApplicationNamespace(manifest, context)}.{name}";
            var route = "/" + name.ToLowerInvariant() + "s";

            var usings = string.Join(Environment.NewLine,
                $"using {feature};",
                $"using {feature}.Create{name};",
                $"using {feature}.Delete{name};",
                $"using {feature}.DTOs;",
                $"using {feature}.Get{name}ById;",
                $"using {feature}.List{name};",
                $"using {feature}.Update{name};",
                "using Truss.Application;");

            if (context is not null)
                usings += $"{Environment.NewLine}using {InfrastructureNamespace(manifest, context)};";

            if (!SourceEditor.InsertAfter(program, $"using {manifest.Name}.Application;", usings))
                log($"Could not update Program.cs usings automatically. Add: {usings.Replace(Environment.NewLine, " ")}");

            var registration = $"builder.Services.AddScoped<I{name}Repository, Ef{name}Repository>();";

            if (!SourceEditor.InsertBefore(program, "var app = builder.Build();", registration))
                log($"Could not update Program.cs automatically. Add before building the app: {registration}");

            var routes = $$"""
                app.MapCommand<Create{{name}}, Guid>("{{route}}", id => $"{{route}}/{id}");
                app.MapQuery<Get{{name}}ById, {{name}}Dto?>("{{route}}/{id:guid}");
                app.MapQuery<List{{name}}, PageResult<{{name}}Dto>>("{{route}}");
                app.MapCommand<Update{{name}}>("{{route}}/update");
                app.MapCommand<Delete{{name}}>("{{route}}/delete");
                """;

            if (!SourceEditor.InsertBefore(program, "app.Run();", routes))
            {
                log("Could not update Program.cs automatically. Add before app.Run():");
                log(routes);
            }
        }

        public static IReadOnlyList<string> GenerateCommand(TrussManifest manifest, string root, string name, string? context)
        {
            ValidateType(name);

            var ns = $"{ApplicationNamespace(manifest, context)}.{name}";
            var directory = Path.Combine(TargetDirectory(root, manifest.ApplicationProject, context), name);

            return
            [
                WriteFile(directory, $"{name}.cs", Render(GeneratorTemplates.Command, name, ns, null)),
                WriteFile(directory, $"{name}Handler.cs", Render(GeneratorTemplates.CommandHandler, name, ns, null)),
                WriteFile(directory, $"{name}Validator.cs", Render(GeneratorTemplates.CommandValidator, name, ns, null))
            ];
        }

        public static IReadOnlyList<string> GenerateQuery(TrussManifest manifest, string root, string name, string? context, string result, bool paged = false)
        {
            ValidateType(name);

            var ns = $"{ApplicationNamespace(manifest, context)}.{name}";
            var directory = Path.Combine(TargetDirectory(root, manifest.ApplicationProject, context), name);

            if (paged)
            {
                return
                [
                    WriteFile(directory, $"{name}.cs", Render(GeneratorTemplates.QueryPaged, name, ns, result)),
                    WriteFile(directory, $"{name}Handler.cs", Render(GeneratorTemplates.QueryPagedHandler, name, ns, result)),
                    WriteFile(directory, $"{name}Validator.cs", Render(GeneratorTemplates.QueryPagedValidator, name, ns, result))
                ];
            }

            return
            [
                WriteFile(directory, $"{name}.cs", Render(GeneratorTemplates.Query, name, ns, result)),
                WriteFile(directory, $"{name}Handler.cs", Render(GeneratorTemplates.QueryHandler, name, ns, result))
            ];
        }

        private static void ValidateType(string name)
        {
            if (!Naming.IsValidTypeName(name))
                throw new ArgumentException($"'{name}' is not a valid name. Use letters and digits, starting with a letter.");
        }

        private static string DomainNamespace(TrussManifest manifest, string? context)
        {
            return context is null ? $"{manifest.Name}.Domain" : $"{manifest.Name}.Domain.{context}";
        }

        private static string ApplicationNamespace(TrussManifest manifest, string? context)
        {
            return context is null ? $"{manifest.Name}.Application" : $"{manifest.Name}.Application.{context}";
        }

        private static string TargetDirectory(string root, string project, string? context)
        {
            if (context is not null)
                ValidateType(context);

            return context is null
                ? Path.Combine(root, project)
                : Path.Combine(root, project, context);
        }

        private static string InfrastructureNamespace(TrussManifest manifest, string? context)
        {
            return context is null ? $"{manifest.Name}.Infrastructure" : $"{manifest.Name}.Infrastructure.{context}";
        }

        /// <summary>
        /// Renders a template whose namespaces mirror the folders: the aggregate
        /// owns a namespace under its context, with ValueObjects, Events and Rules
        /// beneath it, and each crud command or query owns one under the feature.
        /// The owner differs from the name for entities nested in an aggregate.
        /// </summary>
        private static string RenderRich(string template, TrussManifest manifest, string name, string? context, string? owner = null)
        {
            return template
                .Replace("__NS_AGG__", $"{DomainNamespace(manifest, context)}.{owner ?? name}")
                .Replace("__NS_FEATURE__", $"{ApplicationNamespace(manifest, context)}.{owner ?? name}")
                .Replace("__NS_INFRASTRUCTURE__", InfrastructureNamespace(manifest, context))
                .Replace("__TYPE__", name)
                .Replace("__CAMEL__", char.ToLowerInvariant(name[0]) + name[1..]);
        }

        private static bool HasTests(TrussManifest manifest, string root, string project)
        {
            return manifest.Tests && Directory.Exists(Path.Combine(root, project));
        }

        private static string RenderTest(string template, TrussManifest manifest, string name, string? context)
        {
            var domainTests = context is null ? $"{manifest.Name}.Domain.Tests" : $"{manifest.Name}.Domain.Tests.{context}";
            var integrationTests = context is null ? $"{manifest.Name}.IntegrationTests" : $"{manifest.Name}.IntegrationTests.{context}";

            var rendered = RenderRich(template, manifest, name, context)
                .Replace("__NS_DOMAIN_TESTS__", domainTests)
                .Replace("__NS_INTEGRATION_TESTS__", integrationTests)
                .Replace("__NAME__", manifest.Name);

            return DedupeUsings(rendered);
        }

        /// <summary>
        /// Without a context the layer namespaces collapse into the root ones and
        /// a rendered file would repeat a using; keep the first of each.
        /// </summary>
        internal static string DedupeUsings(string content)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var lines = new List<string>();

            foreach (var raw in content.Split('\n'))
            {
                var line = raw.TrimEnd('\r');

                if (line.StartsWith("using ", StringComparison.Ordinal) && !seen.Add(line))
                    continue;

                lines.Add(line);
            }

            return string.Join('\n', lines);
        }

        private static string Render(string template, string type, string ns, string? result)
        {
            var rendered = template
                .Replace("__NS_DOMAIN__", ns)
                .Replace("__NS_APPLICATION__", ns)
                .Replace("__TYPE__", type);

            if (result is not null)
                rendered = rendered.Replace("__RESULT__", result);

            return rendered;
        }

        private static string WriteFile(string directory, string fileName, string content)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);

            if (File.Exists(path))
                throw new InvalidOperationException($"File {path} already exists.");

            File.WriteAllText(path, content + Environment.NewLine);
            return path;
        }
    }
}
