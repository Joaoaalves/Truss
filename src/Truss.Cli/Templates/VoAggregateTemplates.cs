using System.Text;

namespace Truss.Cli.Templates
{
    /// <summary>
    /// The members of an aggregate or entity built over value objects instead of
    /// primitives, and the crud slice that speaks them. The boundary stays
    /// primitive: commands carry strings and numbers, handlers convert through
    /// each value object's Create, and the invariants run before anything is
    /// stored.
    /// </summary>
    internal sealed record VoAggregateModel(
        string Name,
        string AggNs,
        string FeatureNs,
        string InfraNs,
        IReadOnlyList<VoField> Fields)
    {
        public string Camel => char.ToLowerInvariant(Name[0]) + Name[1..];

        public string VoType(VoField field) => field.ReferenceType
            ?? (field.Property.StartsWith(Name, StringComparison.Ordinal) ? field.Property : Name + field.Property);

        public string VoNs(VoField field) => field.ReferenceNamespace ?? $"{AggNs}.ValueObjects";

        public IEnumerable<string> VoUsings() =>
            Fields.Select(field => $"using {VoNs(field)};").Distinct().OrderBy(line => line, StringComparer.Ordinal);

        /// <summary>
        /// The mutator each field gets in the crud slice: Rename for the Name,
        /// Change* for the rest.
        /// </summary>
        public string Mutator(VoField field) => field.Property == "Name" ? "Rename" : $"Change{field.Property}";

        /// <summary>
        /// The field generated tests assert on: the first one whose sample is a
        /// comparable literal.
        /// </summary>
        public VoField? AssertedField() => Fields.FirstOrDefault(field => !field.IsGuid);
    }

    internal static class VoAggregateTemplates
    {
        public static string Aggregate(VoAggregateModel model, bool crud)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"using {model.AggNs}.Events;");
            builder.AppendLine($"using {model.AggNs}.Rules;");
            builder.AppendLine($"using {model.AggNs}.ValueObjects;");

            foreach (var line in model.VoUsings())
                builder.AppendLine(line);

            builder.AppendLine("using Truss.Domain;");
            builder.AppendLine();
            builder.AppendLine($"namespace {model.AggNs}");
            builder.AppendLine("{");
            builder.AppendLine($"    public class {model.Name} : AggregateRoot<{model.Name}Id>");
            builder.AppendLine("    {");
            builder.AppendLine($"        private {model.Name}()");
            builder.AppendLine("        {");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        private {model.Name}({model.Name}Id id, {VoParameters(model)}) : base(id)");
            builder.AppendLine("        {");

            foreach (var field in model.Fields)
                builder.AppendLine($"            {field.Property} = {field.Camel};");

            builder.AppendLine("        }");
            builder.AppendLine();

            foreach (var field in model.Fields)
                builder.AppendLine($"        public {model.VoType(field)} {field.Property} {{ get; private set; }} = default!;").AppendLine();

            builder.AppendLine($"        public static {model.Name} Create({VoParameters(model)})");
            builder.AppendLine("        {");
            builder.AppendLine($"            CheckRule(new {model.Name}MustBeValid());");
            builder.AppendLine();
            builder.AppendLine($"            var instance = new {model.Name}(new {model.Name}Id(Guid.NewGuid()), {string.Join(", ", model.Fields.Select(field => field.Camel))});");
            builder.AppendLine($"            instance.AddDomainEvent(new {model.Name}Created(instance.Id));");
            builder.AppendLine("            return instance;");
            builder.AppendLine("        }");

            if (crud)
            {
                builder.AppendLine();
                builder.AppendLine("        // Model real changes as intention-revealing methods like these");
                builder.AppendLine("        // instead of property setters.");

                foreach (var field in model.Fields)
                {
                    builder.AppendLine($"        public void {model.Mutator(field)}({model.VoType(field)} {field.Camel})");
                    builder.AppendLine("        {");
                    builder.AppendLine($"            {field.Property} = {field.Camel};");
                    builder.AppendLine("        }");
                    builder.AppendLine();
                }

                builder.Length -= Environment.NewLine.Length;
            }

            builder.AppendLine("    }");
            builder.Append('}');

            return builder.ToString();
        }

        public static string Entity(VoAggregateModel model)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"using {model.AggNs}.ValueObjects;");

            foreach (var line in model.VoUsings())
                builder.AppendLine(line);

            builder.AppendLine("using Truss.Domain;");
            builder.AppendLine();
            builder.AppendLine($"namespace {model.AggNs}");
            builder.AppendLine("{");
            builder.AppendLine($"    public class {model.Name} : Entity<{model.Name}Id>");
            builder.AppendLine("    {");
            builder.AppendLine($"        private {model.Name}()");
            builder.AppendLine("        {");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        public {model.Name}({model.Name}Id id, {VoParameters(model)}) : base(id)");
            builder.AppendLine("        {");

            foreach (var field in model.Fields)
                builder.AppendLine($"            {field.Property} = {field.Camel};");

            builder.AppendLine("        }");
            builder.AppendLine();

            foreach (var field in model.Fields)
                builder.AppendLine($"        public {model.VoType(field)} {field.Property} {{ get; private set; }} = default!;").AppendLine();

            builder.Length -= Environment.NewLine.Length;
            builder.AppendLine("    }");
            builder.Append('}');

            return builder.ToString();
        }

        public static string Dto(VoAggregateModel model)
        {
            return $$"""
                namespace {{model.FeatureNs}}.DTOs
                {
                    public sealed record {{model.Name}}Dto(Guid Id, {{PrimitiveParameters(model, pascal: true)}});
                }
                """;
        }

        public static string CreateCommand(VoAggregateModel model)
        {
            return $$"""
                namespace {{model.FeatureNs}}.Create{{model.Name}}
                {
                    using Truss.Application;

                    public sealed record Create{{model.Name}}({{PrimitiveParameters(model, pascal: true)}}) : ICommand<Guid>;
                }
                """;
        }

        public static string CreateHandler(VoAggregateModel model)
        {
            var conversions = string.Join($",{Environment.NewLine}                ",
                model.Fields.Select(field => $"{model.VoType(field)}.Create(command.{field.Property})"));

            return $$"""
                namespace {{model.FeatureNs}}.Create{{model.Name}}
                {
                    using {{model.AggNs}};
                {{IndentedVoUsings(model)}}
                    using {{model.FeatureNs}};
                    using Truss.Application;

                    public class Create{{model.Name}}Handler(I{{model.Name}}Repository repository) : ICommandHandler<Create{{model.Name}}, Guid>
                    {
                        public Task<Guid> Handle(Create{{model.Name}} command, CancellationToken cancellationToken)
                        {
                            var {{model.Camel}} = {{model.Name}}.Create(
                                {{conversions}});

                            repository.Add({{model.Camel}});
                            return Task.FromResult({{model.Camel}}.Id.Value);
                        }
                    }
                }
                """;
        }

        public static string CreateValidator(VoAggregateModel model)
        {
            return Validator(model, $"Create{model.Name}", includeId: false);
        }

        public static string UpdateCommand(VoAggregateModel model)
        {
            return $$"""
                namespace {{model.FeatureNs}}.Update{{model.Name}}
                {
                    using Truss.Application;

                    public sealed record Update{{model.Name}}(Guid Id, {{PrimitiveParameters(model, pascal: true)}}) : ICommand;
                }
                """;
        }

        public static string UpdateHandler(VoAggregateModel model)
        {
            var mutations = string.Join(Environment.NewLine,
                model.Fields.Select(field =>
                    $"            {model.Camel}.{model.Mutator(field)}({model.VoType(field)}.Create(command.{field.Property}));"));

            return $$"""
                namespace {{model.FeatureNs}}.Update{{model.Name}}
                {
                    using {{model.AggNs}}.ValueObjects;
                {{IndentedVoUsings(model)}}
                    using {{model.FeatureNs}};
                    using {{model.FeatureNs}}.Rules;
                    using Truss.Application;
                    using Truss.Domain;

                    public class Update{{model.Name}}Handler(I{{model.Name}}Repository repository) : ICommandHandler<Update{{model.Name}}>
                    {
                        public async Task<Unit> Handle(Update{{model.Name}} command, CancellationToken cancellationToken)
                        {
                            var {{model.Camel}} = await repository.GetById(new {{model.Name}}Id(command.Id), cancellationToken);

                            if ({{model.Camel}} is null)
                                throw new BusinessRuleValidationException(new {{model.Name}}MustExist());

                {{mutations}}

                            return Unit.Value;
                        }
                    }
                }
                """;
        }

        public static string UpdateValidator(VoAggregateModel model)
        {
            return Validator(model, $"Update{model.Name}", includeId: true);
        }

        public static string GetByIdHandler(VoAggregateModel model)
        {
            return $$"""
                namespace {{model.FeatureNs}}.Get{{model.Name}}ById
                {
                    using {{model.AggNs}}.ValueObjects;
                    using {{model.FeatureNs}};
                    using {{model.FeatureNs}}.DTOs;
                    using Truss.Application;

                    public class Get{{model.Name}}ByIdHandler(I{{model.Name}}Repository repository) : IQueryHandler<Get{{model.Name}}ById, {{model.Name}}Dto?>
                    {
                        public async Task<{{model.Name}}Dto?> Handle(Get{{model.Name}}ById query, CancellationToken cancellationToken)
                        {
                            var {{model.Camel}} = await repository.GetById(new {{model.Name}}Id(query.Id), cancellationToken);

                            return {{model.Camel}} is null ? null : new {{model.Name}}Dto({{DtoArguments(model)}});
                        }
                    }
                }
                """;
        }

        public static string Configuration(VoAggregateModel model)
        {
            var conversions = new StringBuilder();

            foreach (var field in model.Fields)
            {
                conversions.AppendLine();
                conversions.AppendLine($"            builder.Property({model.Camel} => {model.Camel}.{field.Property})");

                var maxLength = field.IsString
                    ? $"{Environment.NewLine}                .HasMaxLength({model.VoType(field)}.MaxLength){Environment.NewLine}                .IsRequired();"
                    : ";";

                conversions.Append($"                .HasConversion({field.Camel} => {field.Camel}.Value, value => {model.VoType(field)}.Create(value))");
                conversions.AppendLine(maxLength);
            }

            var usings = string.Join(Environment.NewLine, model.VoUsings());

            return $$"""
                using {{model.AggNs}};
                using {{model.AggNs}}.ValueObjects;
                {{usings}}
                using Microsoft.EntityFrameworkCore;
                using Microsoft.EntityFrameworkCore.Metadata.Builders;

                namespace {{model.InfraNs}}
                {
                    public class {{model.Name}}Configuration : IEntityTypeConfiguration<{{model.Name}}>
                    {
                        public void Configure(EntityTypeBuilder<{{model.Name}}> builder)
                        {
                            builder.ToTable("{{model.Name}}s");
                            builder.HasKey({{model.Camel}} => {{model.Camel}}.Id);

                            builder.Property({{model.Camel}} => {{model.Camel}}.Id)
                                .HasConversion(id => id.Value, value => new {{model.Name}}Id(value));
                {{conversions.ToString().TrimEnd()}}
                        }
                    }
                }
                """;
        }

        public static string EfRepository(VoAggregateModel model)
        {
            var order = model.Fields.FirstOrDefault(field => field.IsString && field.Property == "Name") is not null
                ? "Name"
                : "Id";

            var usings = string.Join(Environment.NewLine, model.VoUsings());

            return $$"""
                using {{model.AggNs}};
                using {{model.AggNs}}.ValueObjects;
                {{usings}}
                using {{model.FeatureNs}};
                using {{model.FeatureNs}}.DTOs;
                using Microsoft.EntityFrameworkCore;
                using Truss.Application;

                namespace {{model.InfraNs}}
                {
                    public class Ef{{model.Name}}Repository(AppDbContext context) : I{{model.Name}}Repository
                    {
                        public void Add({{model.Name}} {{model.Camel}})
                        {
                            context.Set<{{model.Name}}>().Add({{model.Camel}});
                        }

                        public Task<{{model.Name}}?> GetById({{model.Name}}Id id, CancellationToken cancellationToken = default)
                        {
                            return context.Set<{{model.Name}}>().FirstOrDefaultAsync({{model.Camel}} => {{model.Camel}}.Id == id, cancellationToken);
                        }

                        public void Remove({{model.Name}} {{model.Camel}})
                        {
                            context.Set<{{model.Name}}>().Remove({{model.Camel}});
                        }

                        public Task<PageResult<{{model.Name}}Dto>> List(PageRequest page, CancellationToken cancellationToken = default)
                        {
                            return context.Set<{{model.Name}}>()
                                .OrderBy({{model.Camel}} => {{model.Camel}}.{{order}})
                                .Select({{model.Camel}} => new {{model.Name}}Dto({{DtoArguments(model)}}))
                                .ToPageAsync(page, cancellationToken);
                        }
                    }
                }
                """;
        }

        public static string AggregateTest(VoAggregateModel model, string testNs, bool crud)
        {
            var creation = string.Join(", ", model.Fields.Select(field => $"{model.VoType(field)}.Create({field.SampleLiteral()})"));
            var asserted = model.AssertedField();

            var builder = new StringBuilder();

            builder.AppendLine($"using {model.AggNs};");
            builder.AppendLine($"using {model.AggNs}.Events;");

            foreach (var line in model.VoUsings())
                builder.AppendLine(line);

            builder.AppendLine("using Xunit;");
            builder.AppendLine();
            builder.AppendLine($"namespace {testNs}");
            builder.AppendLine("{");
            builder.AppendLine($"    public class {model.Name}Tests");
            builder.AppendLine("    {");
            builder.AppendLine("        [Fact]");
            builder.AppendLine("        public void Create_RaisesTheCreationEvent()");
            builder.AppendLine("        {");
            builder.AppendLine($"            var {model.Camel} = {model.Name}.Create({creation});");
            builder.AppendLine();

            if (asserted is not null)
                builder.AppendLine($"            Assert.Equal({asserted.SampleLiteral()}, {model.Camel}.{asserted.Property}.{ValueMember(asserted)});");

            builder.AppendLine($"            Assert.Contains({model.Camel}.DomainEvents, domainEvent => domainEvent is {model.Name}Created);");
            builder.AppendLine("        }");

            if (crud && asserted is not null)
            {
                builder.AppendLine();
                builder.AppendLine("        [Fact]");
                builder.AppendLine($"        public void {model.Mutator(asserted)}_ChangesThe{asserted.Property}()");
                builder.AppendLine("        {");
                builder.AppendLine($"            var {model.Camel} = {model.Name}.Create({creation});");
                builder.AppendLine();
                builder.AppendLine($"            {model.Camel}.{model.Mutator(asserted)}({model.VoType(asserted)}.Create({asserted.SampleLiteral(updated: true)}));");
                builder.AppendLine();
                builder.AppendLine($"            Assert.Equal({asserted.SampleLiteral(updated: true)}, {model.Camel}.{asserted.Property}.{ValueMember(asserted)});");
                builder.AppendLine("        }");
            }

            builder.AppendLine("    }");
            builder.Append('}');

            return builder.ToString();
        }

        public static string IntegrationTest(VoAggregateModel model, string testNs, string projectName)
        {
            var createArguments = string.Join(", ", model.Fields.Select(field => field.SampleLiteral()));
            var updateArguments = string.Join(", ", model.Fields.Select(field => field.SampleLiteral(updated: true)));
            var asserted = model.AssertedField();

            var createdAssert = asserted is not null
                ? $"Assert.Equal({asserted.SampleLiteral()}, created!.{asserted.Property});"
                : "Assert.NotNull(created);";

            var listAssert = asserted is not null
                ? $"Assert.Contains(page.Items, item => item.{asserted.Property} == {asserted.SampleLiteral(updated: true)});"
                : "Assert.NotEmpty(page.Items);";

            return $$"""
                using {{projectName}}.Application;
                using {{projectName}}.Infrastructure;
                using {{model.FeatureNs}};
                using {{model.FeatureNs}}.Create{{model.Name}};
                using {{model.FeatureNs}}.Delete{{model.Name}};
                using {{model.FeatureNs}}.Get{{model.Name}}ById;
                using {{model.FeatureNs}}.List{{model.Name}};
                using {{model.FeatureNs}}.Update{{model.Name}};
                using {{model.InfraNs}};
                using Microsoft.Extensions.DependencyInjection;
                using Truss.Testing;
                using Xunit;

                namespace {{testNs}}
                {
                    public class {{model.Name}}CrudTests
                    {
                        private static Task<TrussTestHost> StartHost()
                        {
                            return TrussTestHost.Start<AppDbContext>(options =>
                            {
                                options.AddAssembly<ApplicationAssemblyMarker>();
                                options.ConfigureServices(services => services.AddScoped<I{{model.Name}}Repository, Ef{{model.Name}}Repository>());
                            });
                        }

                        [Fact]
                        public async Task TheSlice_CreatesReadsUpdatesAndDeletes()
                        {
                            await using var host = await StartHost();

                            var id = await host.Send(new Create{{model.Name}}({{createArguments}}));

                            var created = await host.Send(new Get{{model.Name}}ById(id));
                            {{createdAssert}}

                            await host.Send(new Update{{model.Name}}(id, {{updateArguments}}));

                            var page = await host.Send(new List{{model.Name}}());
                            {{listAssert}}

                            await host.Send(new Delete{{model.Name}}(id));

                            Assert.Null(await host.Send(new Get{{model.Name}}ById(id)));
                        }
                    }
                }
                """;
        }

        private static string ValueMember(VoField field) => "Value";

        private static string Validator(VoAggregateModel model, string command, bool includeId)
        {
            var rules = new StringBuilder();

            if (includeId)
                rules.AppendLine("            RuleFor(command => command.Id).NotEmpty();");

            foreach (var field in model.Fields.Where(field => field.IsString))
            {
                var minimum = field.HasRule(VoRuleKind.MinLength)
                    ? $".MinimumLength({model.VoType(field)}.MinLength)"
                    : string.Empty;

                rules.AppendLine($"            RuleFor(command => command.{field.Property}).NotEmpty(){minimum}.MaximumLength({model.VoType(field)}.MaxLength);");
            }

            var voUsings = model.Fields.Any(field => field.IsString) ? IndentedVoUsings(model, stringsOnly: true) + Environment.NewLine : string.Empty;

            return $$"""
                namespace {{model.FeatureNs}}.{{command}}
                {
                {{voUsings}}    using FluentValidation;

                    public class {{command}}Validator : AbstractValidator<{{command}}>
                    {
                        public {{command}}Validator()
                        {
                {{rules.ToString().TrimEnd()}}
                        }
                    }
                }
                """;
        }

        private static string VoParameters(VoAggregateModel model)
        {
            return string.Join(", ", model.Fields.Select(field => $"{model.VoType(field)} {field.Camel}"));
        }

        private static string PrimitiveParameters(VoAggregateModel model, bool pascal)
        {
            return string.Join(", ", model.Fields.Select(field => $"{field.Primitive} {(pascal ? field.Property : field.Camel)}"));
        }

        private static string DtoArguments(VoAggregateModel model)
        {
            var fields = string.Join(", ", model.Fields.Select(field => $"{model.Camel}.{field.Property}.Value"));
            return $"{model.Camel}.Id.Value, {fields}";
        }

        private static string IndentedVoUsings(VoAggregateModel model, bool stringsOnly = false)
        {
            var fields = stringsOnly ? model.Fields.Where(field => field.IsString).ToList() : model.Fields.ToList();

            var lines = fields
                .Select(field => $"    using {model.VoNs(field)};")
                .Distinct()
                .OrderBy(line => line, StringComparer.Ordinal);

            return string.Join(Environment.NewLine, lines);
        }
    }
}
