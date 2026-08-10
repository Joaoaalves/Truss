using System.Globalization;

namespace Truss.Cli
{
    /// <summary>
    /// One value-typed member parsed from a --vo or --field specification, like
    /// Name:string:3..120. The generated value object wraps the primitive and
    /// owns the invariants that keep it always valid. A member whose type names
    /// an existing value object becomes a reference to it instead.
    /// </summary>
    internal sealed record VoField(
        string Property,
        string Primitive,
        IReadOnlyList<VoRule> Rules,
        string? ReferenceType = null,
        string? ReferenceNamespace = null)
    {
        private static readonly Dictionary<string, string> Primitives = new(StringComparer.OrdinalIgnoreCase)
        {
            ["string"] = "string",
            ["int"] = "int",
            ["uint"] = "int",
            ["long"] = "long",
            ["decimal"] = "decimal",
            ["double"] = "double",
            ["guid"] = "Guid"
        };

        public string Camel => char.ToLowerInvariant(Property[0]) + Property[1..];

        public bool IsString => Primitive == "string";

        public bool IsGuid => Primitive == "Guid";

        public bool IsReference => ReferenceType is not null;

        public bool HasRule(VoRuleKind kind) => Rules.Any(rule => rule.Kind == kind);

        public decimal RuleBound(VoRuleKind kind) => Rules.First(rule => rule.Kind == kind).Bound;

        /// <summary>
        /// Parses specifications given as repeated options or comma lists. The
        /// shape is Name[:type[:rules]]; the type defaults to string, the rules
        /// segment may take the type's place (Name:3..120), and uint maps to
        /// int: the non-negative rule belongs in the value object, not in a type
        /// that wraps on underflow.
        /// </summary>
        public static List<VoField> Parse(
            IEnumerable<string> specs,
            Action<string>? log = null,
            Func<string, (string Type, string Ns)?>? resolveReference = null)
        {
            var fields = new List<VoField>();

            var flattened = specs.SelectMany(spec =>
                spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            foreach (var spec in flattened)
            {
                var parts = spec.Split(':', 3, StringSplitOptions.TrimEntries);
                var name = parts[0];
                var type = parts.Length > 1 ? parts[1] : "string";
                var rules = parts.Length > 2 ? parts[2] : null;

                if (parts.Length == 2 && VoRule.LooksLikeRules(type))
                {
                    rules = type;
                    type = "string";
                }

                if (!Naming.IsValidTypeName(name))
                    throw new ArgumentException($"'{name}' is not a valid member name. Use letters and digits, starting with a letter.");

                var property = char.ToUpperInvariant(name[0]) + name[1..];

                if (fields.Any(field => field.Property == property))
                    throw new ArgumentException($"The member {property} was specified twice.");

                if (!Primitives.TryGetValue(type, out var primitive))
                {
                    if (resolveReference?.Invoke(type) is { } reference)
                    {
                        if (rules is not null)
                            throw new ArgumentException($"{property}: {reference.Type} already guards its own invariants; rules belong inside it.");

                        fields.Add(new VoField(property, string.Empty, [], reference.Type, reference.Ns));
                        continue;
                    }

                    throw new ArgumentException(
                        $"'{type}' is not a supported value object type. Use one of: string, int, long, decimal, double, guid, or the name of an existing value object.");
                }

                if (type.Equals("uint", StringComparison.OrdinalIgnoreCase))
                    log?.Invoke($"{name}: uint is not CLS-compliant, so the value object wraps an int and enforces non-negative through its rule.");

                fields.Add(new VoField(property, primitive, VoRule.Resolve(rules, property, primitive)));
            }

            return fields;
        }

        /// <summary>
        /// A sample literal of this primitive that satisfies the resolved rules,
        /// and a second, different one for update paths.
        /// </summary>
        public string SampleLiteral(bool updated = false)
        {
            if (IsGuid)
                return "Guid.NewGuid()";

            if (IsString)
            {
                var floor = HasRule(VoRuleKind.MinLength) ? (int)RuleBound(VoRuleKind.MinLength) : 0;
                var ceiling = (int)RuleBound(VoRuleKind.MaxLength);
                var word = updated ? "Joist" : "Beam";

                if (floor <= word.Length && word.Length <= ceiling)
                    return $"\"{word}\"";

                var length = Math.Max(floor, Math.Min(word.Length, ceiling));
                return $"new string('{(updated ? 'J' : 'B')}', {length})";
            }

            var candidate = Math.Max(NumericFloor(), 10m);
            var top = NumericCeiling();

            if (candidate > top)
                candidate = top;

            if (updated)
            {
                if (candidate + 10 <= top)
                    candidate += 10;
                else if (candidate + 1 <= top)
                    candidate += 1;
                else if (candidate - 1 >= NumericFloor())
                    candidate -= 1;
            }

            return NumericLiteral(candidate);
        }

        /// <summary>
        /// A literal that breaks one of the resolved invariants.
        /// </summary>
        public string InvalidLiteral()
        {
            if (IsGuid)
                return "Guid.Empty";

            if (IsString)
                return "\" \"";

            return NumericLiteral(NumericFloor() - 1);
        }

        private decimal NumericFloor()
        {
            if (HasRule(VoRuleKind.Positive))
                return 1;

            if (HasRule(VoRuleKind.AtLeast))
                return RuleBound(VoRuleKind.AtLeast);

            if (HasRule(VoRuleKind.GreaterThan))
                return RuleBound(VoRuleKind.GreaterThan) + 1;

            return 0;
        }

        private decimal NumericCeiling()
        {
            if (HasRule(VoRuleKind.AtMost))
                return RuleBound(VoRuleKind.AtMost);

            if (HasRule(VoRuleKind.LessThan))
                return RuleBound(VoRuleKind.LessThan) - 1;

            return decimal.MaxValue;
        }

        public string NumericLiteral(decimal value)
        {
            var text = value.ToString(CultureInfo.InvariantCulture);

            return Primitive switch
            {
                "decimal" => $"{text}m",
                "double" when text.Contains('.') => text,
                _ => text
            };
        }
    }
}
