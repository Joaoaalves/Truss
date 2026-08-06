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

        public static IReadOnlyList<string> GenerateAggregate(TrussManifest manifest, string root, string name, string? context)
        {
            ValidateType(name);

            var ns = DomainNamespace(manifest, context);
            var directory = TargetDirectory(root, manifest.DomainProject, context);

            return
            [
                WriteFile(directory, $"{name}Id.cs", Render(GeneratorTemplates.AggregateId, name, ns, null)),
                WriteFile(directory, $"{name}.cs", Render(GeneratorTemplates.Aggregate, name, ns, null)),
                WriteFile(directory, $"{name}Created.cs", Render(GeneratorTemplates.AggregateCreated, name, ns, null))
            ];
        }

        public static IReadOnlyList<string> GenerateCommand(TrussManifest manifest, string root, string name, string? context)
        {
            ValidateType(name);

            var ns = ApplicationNamespace(manifest, context);
            var directory = TargetDirectory(root, manifest.ApplicationProject, context);

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

            var ns = ApplicationNamespace(manifest, context);
            var directory = TargetDirectory(root, manifest.ApplicationProject, context);

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
