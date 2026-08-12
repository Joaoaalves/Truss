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

        public static IReadOnlyList<string> GenerateAggregate(TrussManifest manifest, string root, string name, string? context, bool crud = false, string[]? valueObjects = null, Action<string>? log = null)
        {
            ValidateType(name);

            if (crud && !manifest.UsesEntityFramework)
                throw new ArgumentException("--crud generates a repository over the database and requires one. Scaffold the project with --database first.");

            var fields = VoField.Parse(valueObjects ?? [], log, type => ResolveValueObject(manifest, root, type));

            if (crud && fields.Any(field => field.IsReference))
                throw new ArgumentException("--crud cannot flatten a referenced value object into the slice yet. Generate without --crud, or use primitive members.");

            var folder = Path.Combine(TargetDirectory(root, manifest.DomainProject, context), name);

            var files = new List<string>
            {
                WriteFile(Path.Combine(folder, "ValueObjects"), $"{name}Id.cs", RenderRich(GeneratorTemplates.AggregateId, manifest, name, context)),
                WriteFile(Path.Combine(folder, "Events"), $"{name}Created.cs", RenderRich(GeneratorTemplates.AggregateCreated, manifest, name, context)),
                WriteFile(Path.Combine(folder, "Rules"), $"{name}MustBeValid.cs", RenderRich(GeneratorTemplates.AggregateRule, manifest, name, context))
            };

            if (fields.Count == 0)
            {
                files.Insert(0, WriteFile(folder, $"{name}.cs", RenderRich(crud ? GeneratorTemplates.AggregateCrud : GeneratorTemplates.Aggregate, manifest, name, context)));

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

            var model = BuildVoModel(manifest, name, context, fields);

            files.Insert(0, WriteFile(folder, $"{name}.cs", VoAggregateTemplates.Aggregate(model, crud)));
            files.AddRange(GenerateValueObjectFiles(manifest, root, folder, context, model));

            // A referenced value object has an unknowable factory, so the sample
            // construction of the aggregate test cannot be generated.
            if (HasTests(manifest, root, manifest.DomainTestsProject) && !fields.Any(field => field.IsReference))
            {
                files.Add(WriteFile(
                    TargetDirectory(root, manifest.DomainTestsProject, context),
                    $"{name}Tests.cs",
                    VoAggregateTemplates.AggregateTest(model, DomainTestsNamespace(manifest, context), crud)));
            }

            if (crud)
            {
                files.AddRange(GenerateVoCrud(manifest, root, name, context, model));

                if (HasTests(manifest, root, manifest.IntegrationTestsProject))
                {
                    files.Add(WriteFile(
                        TargetDirectory(root, manifest.IntegrationTestsProject, context),
                        $"{name}CrudTests.cs",
                        VoAggregateTemplates.IntegrationTest(model, IntegrationTestsNamespace(manifest, context), manifest.Name)));
                }
            }

            return files;
        }

        /// <summary>
        /// Generates a standalone value object. With -f the members are primitive
        /// fields of one class; with --vo each member is a value object of its
        /// own and the class composes them. With --aggregate the value object is
        /// born inside the owning aggregate's folder, ready to be wired in.
        /// </summary>
        public static IReadOnlyList<string> GenerateValueObject(TrussManifest manifest, string root, string name, string? context, string[]? fieldSpecs, string[]? voSpecs, string? aggregate, Action<string>? log = null)
        {
            ValidateType(name);

            if (fieldSpecs is { Length: > 0 } && voSpecs is { Length: > 0 })
                throw new ArgumentException("Use -f for primitive fields or --vo for composed value objects, not both.");

            string parentNs;
            string parentFolder;

            if (aggregate is null)
            {
                parentNs = $"{DomainNamespace(manifest, context)}.ValueObjects";
                parentFolder = Path.Combine(TargetDirectory(root, manifest.DomainProject, context), "ValueObjects");
            }
            else
            {
                ValidateType(aggregate);

                var owner = Path.Combine(TargetDirectory(root, manifest.DomainProject, context), aggregate);

                if (!Directory.Exists(owner))
                    throw new ArgumentException($"No {aggregate} was found to own {name}. Generate it first: truss g agg {aggregate}{(context is null ? string.Empty : $" -c {context}")} (or g ent for an entity).");

                parentNs = $"{DomainNamespace(manifest, context)}.{aggregate}.ValueObjects";
                parentFolder = Path.Combine(owner, "ValueObjects");
            }

            var ns = parentNs;
            var folder = Path.Combine(parentFolder, name);
            var files = new List<string>();

            if (voSpecs is { Length: > 0 })
            {
                var members = VoField.Parse(voSpecs, log);

                files.Add(WriteFile(folder, $"{name}.cs", ValueObjectTemplates.CompositeClass(parentNs, name, members)));

                // The members are ordinary value objects beside the composite,
                // so the aggregate (or anything else) can also speak them alone.
                foreach (var member in members)
                {
                    var memberFolder = Path.Combine(parentFolder, member.Property);
                    var single = new List<VoField> { member with { Property = "Value" } };

                    files.Add(WriteFile(memberFolder, $"{member.Property}.cs", ValueObjectTemplates.ValueObjectClass(parentNs, member.Property, single)));

                    foreach (var (fileName, content) in ValueObjectTemplates.RuleFiles(parentNs, member.Property, single))
                        files.Add(WriteFile(Path.Combine(memberFolder, "Rules"), fileName, content));
                }

                if (HasTests(manifest, root, manifest.DomainTestsProject))
                {
                    files.Add(WriteFile(
                        TargetDirectory(root, manifest.DomainTestsProject, context),
                        $"{name}Tests.cs",
                        ValueObjectTemplates.TestFile(DomainTestsNamespace(manifest, context), ns, name, members, composite: true)));
                }
            }
            else
            {
                var fields = VoField.Parse(fieldSpecs is { Length: > 0 } ? fieldSpecs : ["Value:string"], log);

                files.Add(WriteFile(folder, $"{name}.cs", ValueObjectTemplates.ValueObjectClass(ns, name, fields)));

                foreach (var (fileName, content) in ValueObjectTemplates.RuleFiles(ns, name, fields))
                    files.Add(WriteFile(Path.Combine(folder, "Rules"), fileName, content));

                if (HasTests(manifest, root, manifest.DomainTestsProject))
                {
                    files.Add(WriteFile(
                        TargetDirectory(root, manifest.DomainTestsProject, context),
                        $"{name}Tests.cs",
                        ValueObjectTemplates.TestFile(DomainTestsNamespace(manifest, context), ns, name, fields)));
                }
            }

            if (aggregate is not null)
                LogOwnerWiring(manifest, root, context, aggregate, name, ns, log);

            return files;
        }

        /// <summary>
        /// Prints the lines that wire a generated value object into its owner,
        /// phrased for what the owner actually is: aggregates take it through
        /// Create, entities through their constructor.
        /// </summary>
        private static void LogOwnerWiring(TrussManifest manifest, string root, string? context, string owner, string name, string ns, Action<string>? log)
        {
            var ownerFile = Path.Combine(TargetDirectory(root, manifest.DomainProject, context), owner, $"{owner}.cs");
            var ownerSource = File.Exists(ownerFile) ? File.ReadAllText(ownerFile) : string.Empty;

            var entry = ownerSource.Contains("AggregateRoot<", StringComparison.Ordinal)
                ? $"{owner}.Create"
                : ownerSource.Contains("Entity<", StringComparison.Ordinal)
                    ? $"the {owner} constructor"
                    : $"{owner}'s factory or constructor";

            log?.Invoke($"Wire it into {owner} (properties are yours to name):");
            log?.Invoke($"    using {ns};");
            log?.Invoke($"    public {name} {name} {{ get; private set; }} = default!;");
            log?.Invoke($"    Add a {name} parameter to {entry} and assign it.");
        }

        /// <summary>
        /// Finds an existing value object by type name so a --vo member can
        /// reference it instead of generating a wrapper.
        /// </summary>
        private static (string Type, string Ns)? ResolveValueObject(TrussManifest manifest, string root, string type)
        {
            var domainRoot = Path.Combine(root, manifest.DomainProject);

            if (!Directory.Exists(domainRoot))
                return null;

            var file = Directory.EnumerateFiles(domainRoot, $"{type}.cs", SearchOption.AllDirectories)
                .FirstOrDefault(candidate => File.ReadAllText(candidate).Contains(": ValueObject"));

            if (file is null)
                return null;

            var ns = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(file), @"namespace\s+([\w.]+)").Groups[1].Value;

            return (type, ns);
        }

        public static IReadOnlyList<string> GenerateEntity(TrussManifest manifest, string root, string name, string? context, string? aggregate, string[]? valueObjects = null, Action<string>? log = null)
        {
            ValidateType(name);

            if (aggregate is not null)
                ValidateType(aggregate);

            var fields = VoField.Parse(valueObjects ?? [], log, type => ResolveValueObject(manifest, root, type));
            var owner = aggregate ?? name;
            var folder = Path.Combine(TargetDirectory(root, manifest.DomainProject, context), owner);

            var files = new List<string>
            {
                WriteFile(Path.Combine(folder, "ValueObjects"), $"{name}Id.cs", RenderRich(GeneratorTemplates.AggregateId, manifest, name, context, owner))
            };

            if (fields.Count == 0)
            {
                files.Insert(0, WriteFile(folder, $"{name}.cs", RenderRich(GeneratorTemplates.Entity, manifest, name, context, owner)));
                return files;
            }

            var ownerNs = $"{DomainNamespace(manifest, context)}.{owner}";
            var model = new VoAggregateModel(name, ownerNs, string.Empty, string.Empty, fields);

            files.Insert(0, WriteFile(folder, $"{name}.cs", VoAggregateTemplates.Entity(model)));
            files.AddRange(GenerateValueObjectFiles(manifest, root, folder, context, model));

            return files;
        }

        private static VoAggregateModel BuildVoModel(TrussManifest manifest, string name, string? context, List<VoField> fields)
        {
            return new VoAggregateModel(
                name,
                $"{DomainNamespace(manifest, context)}.{name}",
                $"{ApplicationNamespace(manifest, context)}.{name}",
                InfrastructureNamespace(manifest, context),
                fields);
        }

        /// <summary>
        /// The value objects a rich aggregate or entity owns: one class per field
        /// in its own folder with its rules, plus a test when the project has the
        /// test projects.
        /// </summary>
        private static IEnumerable<string> GenerateValueObjectFiles(TrussManifest manifest, string root, string folder, string? context, VoAggregateModel model)
        {
            foreach (var field in model.Fields.Where(field => !field.IsReference))
            {
                var voType = model.VoType(field);
                var voNs = model.VoNs(field);
                var voFolder = Path.Combine(folder, "ValueObjects", voType);
                var single = new List<VoField> { field with { Property = "Value" } };

                yield return WriteFile(voFolder, $"{voType}.cs", ValueObjectTemplates.ValueObjectClass(voNs, voType, single));

                foreach (var (fileName, content) in ValueObjectTemplates.RuleFiles(voNs, voType, single))
                    yield return WriteFile(Path.Combine(voFolder, "Rules"), fileName, content);

                if (HasTests(manifest, root, manifest.DomainTestsProject))
                {
                    yield return WriteFile(
                        TargetDirectory(root, manifest.DomainTestsProject, context),
                        $"{voType}Tests.cs",
                        ValueObjectTemplates.TestFile(DomainTestsNamespace(manifest, context), voNs, voType, single));
                }
            }
        }

        /// <summary>
        /// The crud slice over a rich aggregate. The shapes that never touch the
        /// fields reuse the plain templates; the ones that do are built over the
        /// value objects, converting at the boundary.
        /// </summary>
        private static IEnumerable<string> GenerateVoCrud(TrussManifest manifest, string root, string name, string? context, VoAggregateModel model)
        {
            var feature = Path.Combine(TargetDirectory(root, manifest.ApplicationProject, context), name);
            var infrastructure = TargetDirectory(root, manifest.InfrastructureProject, context);

            yield return WriteFile(Path.Combine(feature, "DTOs"), $"{name}Dto.cs", VoAggregateTemplates.Dto(model));
            yield return WriteFile(feature, $"I{name}Repository.cs", RenderRich(GeneratorTemplates.CrudRepository, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, "Rules"), $"{name}MustExist.cs", RenderRich(GeneratorTemplates.CrudMustExist, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Create{name}"), $"Create{name}.cs", VoAggregateTemplates.CreateCommand(model));
            yield return WriteFile(Path.Combine(feature, $"Create{name}"), $"Create{name}Handler.cs", VoAggregateTemplates.CreateHandler(model));
            yield return WriteFile(Path.Combine(feature, $"Create{name}"), $"Create{name}Validator.cs", VoAggregateTemplates.CreateValidator(model));
            yield return WriteFile(Path.Combine(feature, $"Update{name}"), $"Update{name}.cs", VoAggregateTemplates.UpdateCommand(model));
            yield return WriteFile(Path.Combine(feature, $"Update{name}"), $"Update{name}Handler.cs", VoAggregateTemplates.UpdateHandler(model));
            yield return WriteFile(Path.Combine(feature, $"Update{name}"), $"Update{name}Validator.cs", VoAggregateTemplates.UpdateValidator(model));
            yield return WriteFile(Path.Combine(feature, $"Delete{name}"), $"Delete{name}.cs", RenderRich(GeneratorTemplates.CrudDelete, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Delete{name}"), $"Delete{name}Handler.cs", RenderRich(GeneratorTemplates.CrudDeleteHandler, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Get{name}ById"), $"Get{name}ById.cs", RenderRich(GeneratorTemplates.CrudGetById, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"Get{name}ById"), $"Get{name}ByIdHandler.cs", VoAggregateTemplates.GetByIdHandler(model));
            yield return WriteFile(Path.Combine(feature, $"List{name}"), $"List{name}.cs", RenderRich(GeneratorTemplates.CrudList, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"List{name}"), $"List{name}Handler.cs", RenderRich(GeneratorTemplates.CrudListHandler, manifest, name, context));
            yield return WriteFile(Path.Combine(feature, $"List{name}"), $"List{name}Validator.cs", RenderRich(GeneratorTemplates.CrudListValidator, manifest, name, context));
            yield return WriteFile(infrastructure, $"{name}Configuration.cs", VoAggregateTemplates.Configuration(model));
            yield return WriteFile(infrastructure, $"Ef{name}Repository.cs", VoAggregateTemplates.EfRepository(model));
        }

        private static string DomainTestsNamespace(TrussManifest manifest, string? context)
        {
            return context is null ? $"{manifest.Name}.Domain.Tests" : $"{manifest.Name}.Domain.Tests.{context}";
        }

        private static string IntegrationTestsNamespace(TrussManifest manifest, string? context)
        {
            return context is null ? $"{manifest.Name}.IntegrationTests" : $"{manifest.Name}.IntegrationTests.{context}";
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

            if (!SourceEditor.InsertAtMarker(program, Markers.Services, registration)
                && !SourceEditor.InsertBefore(program, "var app = builder.Build();", registration))
            {
                log($"Could not update Program.cs automatically. Add before building the app: {registration}");
            }

            var routes = $$"""
                app.MapCommand<Create{{name}}, Guid>("{{route}}", id => $"{{route}}/{id}");
                app.MapQuery<Get{{name}}ById, {{name}}Dto?>("{{route}}/{id:guid}");
                app.MapQuery<List{{name}}, PageResult<{{name}}Dto>>("{{route}}");
                app.MapCommand<Update{{name}}>("{{route}}/update");
                app.MapCommand<Delete{{name}}>("{{route}}/delete");
                """;

            if (!SourceEditor.InsertAtMarker(program, Markers.Endpoints, routes)
                && !SourceEditor.InsertBefore(program, "app.Run();", routes))
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

            var files = new List<string>();
            var dtoUsing = string.Empty;

            // A query that answers with a DTO gets the DTO: a handler pointing at a
            // type nobody wrote does not compile.
            if (NeedsDto(manifest, root, context, result))
            {
                var dtoNamespace = $"{ApplicationNamespace(manifest, context)}.DTOs";

                files.Add(WriteFile(
                    Path.Combine(TargetDirectory(root, manifest.ApplicationProject, context), "DTOs"),
                    $"{result}.cs",
                    GeneratorTemplates.QueryDto.Replace("__NS_DTOS__", dtoNamespace).Replace("__RESULT__", result)));

                dtoUsing = $"{Environment.NewLine}    using {dtoNamespace};";
            }

            if (paged)
            {
                files.Add(WriteFile(directory, $"{name}.cs", Render(GeneratorTemplates.QueryPaged, name, ns, result, dtoUsing)));
                files.Add(WriteFile(directory, $"{name}Handler.cs", Render(GeneratorTemplates.QueryPagedHandler, name, ns, result, dtoUsing)));
                files.Add(WriteFile(directory, $"{name}Validator.cs", Render(GeneratorTemplates.QueryPagedValidator, name, ns, result)));

                return files;
            }

            files.Add(WriteFile(directory, $"{name}.cs", Render(GeneratorTemplates.Query, name, ns, result, dtoUsing)));
            files.Add(WriteFile(directory, $"{name}Handler.cs", Render(GeneratorTemplates.QueryHandler, name, ns, result, dtoUsing)));

            return files;
        }

        private static readonly string[] KnownResultTypes =
        [
            "string", "int", "long", "decimal", "double", "bool", "Guid",
            "DateOnly", "DateTime", "DateTimeOffset", "TimeSpan", "object"
        ];

        /// <summary>
        /// Whether the result type names something the project does not have yet,
        /// in which case the generator writes its skeleton.
        /// </summary>
        private static bool NeedsDto(TrussManifest manifest, string root, string? context, string result)
        {
            if (KnownResultTypes.Contains(result, StringComparer.OrdinalIgnoreCase) || !Naming.IsValidTypeName(result))
                return false;

            var application = Path.Combine(root, manifest.ApplicationProject);

            return !Directory.Exists(application)
                || !Directory.EnumerateFiles(application, $"{result}.cs", SearchOption.AllDirectories).Any();
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
                var directive = line.TrimStart();

                if (directive.StartsWith("using ", StringComparison.Ordinal) && !seen.Add(directive))
                    continue;

                lines.Add(line);
            }

            return string.Join('\n', lines);
        }

        private static string Render(string template, string type, string ns, string? result, string dtoUsing = "")
        {
            var rendered = template
                .Replace("__DTO_USING__", dtoUsing)
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
