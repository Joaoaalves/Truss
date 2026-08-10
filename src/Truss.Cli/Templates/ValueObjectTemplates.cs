using System.Text;
using System.Text.RegularExpressions;

namespace Truss.Cli.Templates
{
    /// <summary>
    /// Builds value objects that guard their own invariants: a private
    /// constructor, a Create factory that normalizes and checks rules, and
    /// equality by value. A value object has no identity and raises no events;
    /// if it exists, it is valid.
    /// </summary>
    internal static partial class ValueObjectTemplates
    {
        /// <summary>
        /// The value object class. Fields with a single member named Value render
        /// the wrapper shape (--vo on aggregates); several members render a
        /// composite (truss g vo).
        /// </summary>
        public static string ValueObjectClass(string ns, string voType, IReadOnlyList<VoField> fields)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"using {ns}.Rules;");
            builder.AppendLine("using Truss.Domain;");
            builder.AppendLine();
            builder.AppendLine($"namespace {ns}");
            builder.AppendLine("{");
            builder.AppendLine($"    public sealed class {voType} : ValueObject");
            builder.AppendLine("    {");

            foreach (var field in fields.Where(field => field.IsString))
                builder.AppendLine($"        public const int {MaxLengthConst(field, fields)} = 200;").AppendLine();

            builder.AppendLine($"        private {voType}({CtorParameters(fields)})");
            builder.AppendLine("        {");

            foreach (var field in fields)
                builder.AppendLine($"            {field.Property} = {field.Camel};");

            builder.AppendLine("        }");
            builder.AppendLine();

            foreach (var field in fields)
                builder.AppendLine($"        public {field.Primitive} {field.Property} {{ get; }}").AppendLine();

            builder.AppendLine($"        public static {voType} Create({CtorParameters(fields)})");
            builder.AppendLine("        {");

            foreach (var field in fields.Where(field => field.IsString))
                builder.AppendLine($"            var {Normalized(field, fields)} = {field.Camel}?.Trim() ?? string.Empty;");

            if (fields.Any(field => field.IsString))
                builder.AppendLine();

            foreach (var field in fields)
            {
                var argument = field.IsString ? Normalized(field, fields) : field.Camel;

                foreach (var rule in RuleNames(voType, field, fields))
                    builder.AppendLine($"            CheckRule(new {rule}({argument}));");
            }

            builder.AppendLine();
            builder.AppendLine($"            return new {voType}({CreateArguments(fields)});");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        protected override IEnumerable<object?> GetEqualityComponents()");
            builder.AppendLine("        {");

            foreach (var field in fields)
                builder.AppendLine($"            yield return {field.Property};");

            builder.AppendLine("        }");

            if (fields.Count == 1)
            {
                var value = fields[0].IsString ? fields[0].Property : $"{fields[0].Property}.ToString()";

                builder.AppendLine();
                builder.AppendLine($"        public override string ToString() => {value};");
            }

            builder.AppendLine("    }");
            builder.Append('}');

            return builder.ToString();
        }

        /// <summary>
        /// The starter rules of each member: strings must not be empty and must
        /// fit the length, numbers must not be negative, guids must not be
        /// default. Replace or extend them with the real invariants.
        /// </summary>
        public static IEnumerable<(string FileName, string Content)> RuleFiles(string ns, string voType, IReadOnlyList<VoField> fields)
        {
            foreach (var field in fields)
            {
                var prefix = RulePrefix(voType, field, fields);
                var human = Human(prefix);
                var code = RuleCode(voType, field, fields);

                if (field.IsString)
                {
                    yield return ($"{prefix}MustNotBeEmpty.cs", $$"""
                        using Truss.Domain;

                        namespace {{ns}}.Rules
                        {
                            public class {{prefix}}MustNotBeEmpty(string value) : IBusinessRule
                            {
                                public bool IsBroken() => string.IsNullOrWhiteSpace(value);

                                public string Message => "The {{human}} must not be empty.";

                                public string Code => "{{code}}.empty";
                            }
                        }
                        """);

                    var maxReference = $"{voType}.{MaxLengthConst(field, fields)}";
                    var lengthMessage = "$\"The " + human + " must have at most {" + maxReference + "} characters.\"";

                    yield return ($"{prefix}MustFitLength.cs", $$"""
                        using Truss.Domain;

                        namespace {{ns}}.Rules
                        {
                            public class {{prefix}}MustFitLength(string value) : IBusinessRule
                            {
                                public bool IsBroken() => value.Length > {{maxReference}};

                                public string Message => {{lengthMessage}};

                                public string Code => "{{code}}.too-long";
                            }
                        }
                        """);
                }
                else if (field.IsGuid)
                {
                    yield return ($"{prefix}MustNotBeDefault.cs", $$"""
                        using Truss.Domain;

                        namespace {{ns}}.Rules
                        {
                            public class {{prefix}}MustNotBeDefault(Guid value) : IBusinessRule
                            {
                                public bool IsBroken() => value == Guid.Empty;

                                public string Message => "The {{human}} must be provided.";

                                public string Code => "{{code}}.default";
                            }
                        }
                        """);
                }
                else
                {
                    yield return ($"{prefix}MustNotBeNegative.cs", $$"""
                        using Truss.Domain;

                        namespace {{ns}}.Rules
                        {
                            public class {{prefix}}MustNotBeNegative({{field.Primitive}} value) : IBusinessRule
                            {
                                public bool IsBroken() => value < 0;

                                public string Message => "The {{human}} must not be negative.";

                                public string Code => "{{code}}.negative";
                            }
                        }
                        """);
                }
            }
        }

        /// <summary>
        /// A test per value object: creation keeps (and for strings, normalizes)
        /// the value, and each starter invariant refuses its bad input.
        /// </summary>
        public static string TestFile(string testNs, string voNs, string voType, IReadOnlyList<VoField> fields)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"using {voNs};");
            builder.AppendLine("using Truss.Domain;");
            builder.AppendLine("using Xunit;");
            builder.AppendLine();
            builder.AppendLine($"namespace {testNs}");
            builder.AppendLine("{");
            builder.AppendLine($"    public class {voType}Tests");
            builder.AppendLine("    {");
            builder.AppendLine("        [Fact]");
            builder.AppendLine("        public void Create_KeepsTheValue()");
            builder.AppendLine("        {");
            builder.AppendLine($"            var value = {voType}.Create({string.Join(", ", fields.Select(field => field.SampleLiteral()))});");
            builder.AppendLine();

            var asserted = fields.FirstOrDefault(field => !field.IsGuid);

            if (asserted is not null)
                builder.AppendLine($"            Assert.Equal({asserted.SampleLiteral()}, value.{asserted.Property});");
            else
                builder.AppendLine("            Assert.NotNull(value);");

            builder.AppendLine("        }");

            foreach (var field in fields)
            {
                var arguments = fields
                    .Select(other => other == field ? field.InvalidLiteral() : other.SampleLiteral());

                builder.AppendLine();
                builder.AppendLine("        [Fact]");
                builder.AppendLine($"        public void Create_WithAnInvalid{field.Property}_BreaksTheRule()");
                builder.AppendLine("        {");
                builder.AppendLine($"            Assert.Throws<BusinessRuleValidationException>(() => {voType}.Create({string.Join(", ", arguments)}));");
                builder.AppendLine("        }");
            }

            builder.AppendLine("    }");
            builder.Append('}');

            return builder.ToString();
        }

        public static string RulePrefix(string voType, VoField field, IReadOnlyList<VoField> fields)
        {
            return fields.Count == 1 && field.Property == "Value" ? voType : voType + field.Property;
        }

        /// <summary>
        /// The rule class names checked by Create for one member, in order.
        /// </summary>
        public static IEnumerable<string> RuleNames(string voType, VoField field, IReadOnlyList<VoField> fields)
        {
            var prefix = RulePrefix(voType, field, fields);

            if (field.IsString)
            {
                yield return $"{prefix}MustNotBeEmpty";
                yield return $"{prefix}MustFitLength";
            }
            else if (field.IsGuid)
            {
                yield return $"{prefix}MustNotBeDefault";
            }
            else
            {
                yield return $"{prefix}MustNotBeNegative";
            }
        }

        private static string RuleCode(string voType, VoField field, IReadOnlyList<VoField> fields)
        {
            var voCamel = char.ToLowerInvariant(voType[0]) + voType[1..];

            return fields.Count == 1 && field.Property == "Value" ? voCamel : $"{voCamel}.{field.Camel}";
        }

        public static string MaxLengthConst(VoField field, IReadOnlyList<VoField> fields)
        {
            return fields.Count == 1 && field.Property == "Value" ? "MaxLength" : $"{field.Property}MaxLength";
        }

        private static string CtorParameters(IReadOnlyList<VoField> fields)
        {
            return string.Join(", ", fields.Select(field => $"{field.Primitive} {field.Camel}"));
        }

        private static string CreateArguments(IReadOnlyList<VoField> fields)
        {
            return string.Join(", ", fields.Select(field => field.IsString ? Normalized(field, fields) : field.Camel));
        }

        private static string Normalized(VoField field, IReadOnlyList<VoField> fields)
        {
            return fields.Count == 1 ? "normalized" : $"normalized{field.Property}";
        }

        /// <summary>
        /// Splits a pascal-cased type name into readable words for rule messages.
        /// </summary>
        private static string Human(string pascal)
        {
            return SplitWords().Replace(pascal, " $1").Trim().ToLowerInvariant();
        }

        [GeneratedRegex("([A-Z][a-z0-9]*)")]
        private static partial Regex SplitWords();
    }
}
