using System.Globalization;
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
        /// composite of primitives (truss g vo -f).
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
            {
                if (field.HasRule(VoRuleKind.MinLength))
                    builder.AppendLine($"        public const int {LengthConst(field, fields, VoRuleKind.MinLength)} = {(int)field.RuleBound(VoRuleKind.MinLength)};").AppendLine();

                builder.AppendLine($"        public const int {LengthConst(field, fields, VoRuleKind.MaxLength)} = {(int)field.RuleBound(VoRuleKind.MaxLength)};").AppendLine();
            }

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
        /// A value object composed of other value objects: MacroNutrients built
        /// from Carbohydrates, Fat and Protein. Each member guards itself; the
        /// composite is the home of rules and behavior that read several members,
        /// and its primitive Create overload keeps construction short while every
        /// invariant still runs.
        /// </summary>
        public static string CompositeClass(string parentNs, string voType, IReadOnlyList<VoField> members)
        {
            var builder = new StringBuilder();

            // The members live in sibling namespaces, and the usings sit inside
            // this one so each member type resolves over its same-named namespace.
            builder.AppendLine($"namespace {parentNs}.{voType}");
            builder.AppendLine("{");

            foreach (var member in members.OrderBy(member => member.Property, StringComparer.Ordinal))
                builder.AppendLine($"    using {parentNs}.{member.Property};");

            builder.AppendLine("    using Truss.Domain;");
            builder.AppendLine();
            builder.AppendLine($"    public sealed class {voType} : ValueObject");
            builder.AppendLine("    {");
            builder.AppendLine($"        private {voType}({MemberParameters(members)})");
            builder.AppendLine("        {");

            foreach (var member in members)
                builder.AppendLine($"            {member.Property} = {member.Camel};");

            builder.AppendLine("        }");
            builder.AppendLine();

            foreach (var member in members)
                builder.AppendLine($"        public {member.Property} {member.Property} {{ get; }}").AppendLine();

            builder.AppendLine($"        public static {voType} Create({MemberParameters(members)})");
            builder.AppendLine("        {");
            builder.AppendLine("            // Rules that read several members belong here.");
            builder.AppendLine($"            return new {voType}({string.Join(", ", members.Select(member => member.Camel))});");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine($"        public static {voType} Create({string.Join(", ", members.Select(member => $"{member.Primitive} {member.Camel}"))})");
            builder.AppendLine("        {");
            builder.AppendLine($"            return Create({string.Join(", ", members.Select(member => $"{member.Property}.Create({member.Camel})"))});");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        protected override IEnumerable<object?> GetEqualityComponents()");
            builder.AppendLine("        {");

            foreach (var member in members)
                builder.AppendLine($"            yield return {member.Property};");

            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.Append('}');

            return builder.ToString();
        }

        /// <summary>
        /// The resolved rules of each member as classes to replace or extend
        /// with the real invariants.
        /// </summary>
        public static IEnumerable<(string FileName, string Content)> RuleFiles(string ns, string voType, IReadOnlyList<VoField> fields)
        {
            foreach (var field in fields)
            {
                var prefix = RulePrefix(voType, field, fields);
                var human = Human(prefix);
                var code = RuleCode(voType, field, fields);

                foreach (var rule in field.Rules)
                    yield return RuleFile(ns, voType, field, fields, rule, prefix, human, code);
            }
        }

        private static (string FileName, string Content) RuleFile(
            string ns,
            string voType,
            VoField field,
            IReadOnlyList<VoField> fields,
            VoRule rule,
            string prefix,
            string human,
            string code)
        {
            var (name, parameterType, broken, message, suffix) = rule.Kind switch
            {
                VoRuleKind.NotEmpty => (
                    $"{prefix}MustNotBeEmpty", "string",
                    "string.IsNullOrWhiteSpace(value)",
                    $"\"The {human} must not be empty.\"", "empty"),
                VoRuleKind.MinLength => (
                    $"{prefix}MustNotBeTooShort", "string",
                    $"value.Length < {voType}.{LengthConst(field, fields, VoRuleKind.MinLength)}",
                    LengthMessage(human, voType, LengthConst(field, fields, VoRuleKind.MinLength), "at least"), "too-short"),
                VoRuleKind.MaxLength => (
                    $"{prefix}MustFitLength", "string",
                    $"value.Length > {voType}.{LengthConst(field, fields, VoRuleKind.MaxLength)}",
                    LengthMessage(human, voType, LengthConst(field, fields, VoRuleKind.MaxLength), "at most"), "too-long"),
                VoRuleKind.NonNegative => (
                    $"{prefix}MustNotBeNegative", field.Primitive,
                    "value < 0",
                    $"\"The {human} must not be negative.\"", "negative"),
                VoRuleKind.Positive => (
                    $"{prefix}MustBePositive", field.Primitive,
                    "value <= 0",
                    $"\"The {human} must be positive.\"", "not-positive"),
                VoRuleKind.AtLeast => (
                    $"{prefix}MustBeAtLeast", field.Primitive,
                    $"value < {field.NumericLiteral(rule.Bound)}",
                    $"\"The {human} must be at least {Plain(rule.Bound)}.\"", "too-small"),
                VoRuleKind.GreaterThan => (
                    $"{prefix}MustBeGreaterThan", field.Primitive,
                    $"value <= {field.NumericLiteral(rule.Bound)}",
                    $"\"The {human} must be greater than {Plain(rule.Bound)}.\"", "too-small"),
                VoRuleKind.AtMost => (
                    $"{prefix}MustBeAtMost", field.Primitive,
                    $"value > {field.NumericLiteral(rule.Bound)}",
                    $"\"The {human} must be at most {Plain(rule.Bound)}.\"", "too-large"),
                VoRuleKind.LessThan => (
                    $"{prefix}MustBeLessThan", field.Primitive,
                    $"value >= {field.NumericLiteral(rule.Bound)}",
                    $"\"The {human} must be less than {Plain(rule.Bound)}.\"", "too-large"),
                _ => (
                    $"{prefix}MustNotBeDefault", "Guid",
                    "value == Guid.Empty",
                    $"\"The {human} must be provided.\"", "default")
            };

            var content = $$"""
                using Truss.Domain;

                namespace {{ns}}.Rules
                {
                    public class {{name}}({{parameterType}} value) : IBusinessRule
                    {
                        public bool IsBroken() => {{broken}};

                        public string Message => {{message}};

                        public string Code => "{{code}}.{{suffix}}";
                    }
                }
                """;

            return ($"{name}.cs", content);
        }

        /// <summary>
        /// A test per value object: creation keeps (and for strings, normalizes)
        /// the value, and each member's invariants refuse a bad input.
        /// </summary>
        public static string TestFile(string testNs, string voNs, string voType, IReadOnlyList<VoField> fields, bool composite = false)
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
                builder.AppendLine($"            Assert.Equal({asserted.SampleLiteral()}, value.{asserted.Property}{(composite ? ".Value" : string.Empty)});");
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

            foreach (var rule in field.Rules)
            {
                yield return rule.Kind switch
                {
                    VoRuleKind.NotEmpty => $"{prefix}MustNotBeEmpty",
                    VoRuleKind.MinLength => $"{prefix}MustNotBeTooShort",
                    VoRuleKind.MaxLength => $"{prefix}MustFitLength",
                    VoRuleKind.NonNegative => $"{prefix}MustNotBeNegative",
                    VoRuleKind.Positive => $"{prefix}MustBePositive",
                    VoRuleKind.AtLeast => $"{prefix}MustBeAtLeast",
                    VoRuleKind.GreaterThan => $"{prefix}MustBeGreaterThan",
                    VoRuleKind.AtMost => $"{prefix}MustBeAtMost",
                    VoRuleKind.LessThan => $"{prefix}MustBeLessThan",
                    _ => $"{prefix}MustNotBeDefault"
                };
            }
        }

        private static string RuleCode(string voType, VoField field, IReadOnlyList<VoField> fields)
        {
            var voCamel = char.ToLowerInvariant(voType[0]) + voType[1..];

            return fields.Count == 1 && field.Property == "Value" ? voCamel : $"{voCamel}.{field.Camel}";
        }

        public static string LengthConst(VoField field, IReadOnlyList<VoField> fields, VoRuleKind kind)
        {
            var name = kind == VoRuleKind.MinLength ? "MinLength" : "MaxLength";

            return fields.Count == 1 && field.Property == "Value" ? name : $"{field.Property}{name}";
        }

        private static string LengthMessage(string human, string voType, string constName, string comparison)
        {
            return "$\"The " + human + " must have " + comparison + " {" + voType + "." + constName + "} characters.\"";
        }

        private static string Plain(decimal bound) => bound.ToString(CultureInfo.InvariantCulture);

        private static string CtorParameters(IReadOnlyList<VoField> fields)
        {
            return string.Join(", ", fields.Select(field => $"{field.Primitive} {field.Camel}"));
        }

        private static string MemberParameters(IReadOnlyList<VoField> members)
        {
            return string.Join(", ", members.Select(member => $"{member.Property} {member.Camel}"));
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
