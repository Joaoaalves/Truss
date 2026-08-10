namespace Truss.Cli
{
    /// <summary>
    /// One value-typed member parsed from a --vo or --field specification, like
    /// Name:string. The generated value object wraps the primitive and owns the
    /// invariants that keep it always valid.
    /// </summary>
    internal sealed record VoField(string Property, string Primitive)
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

        /// <summary>
        /// Parses specifications given as repeated options or comma lists. The
        /// type defaults to string, and uint maps to int: the non-negative rule
        /// belongs in the value object, not in a type that wraps on underflow.
        /// </summary>
        public static List<VoField> Parse(IEnumerable<string> specs, Action<string>? log = null)
        {
            var fields = new List<VoField>();

            var flattened = specs.SelectMany(spec =>
                spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

            foreach (var spec in flattened)
            {
                var parts = spec.Split(':', 2, StringSplitOptions.TrimEntries);
                var name = parts[0];
                var type = parts.Length > 1 ? parts[1] : "string";

                if (!Naming.IsValidTypeName(name))
                    throw new ArgumentException($"'{name}' is not a valid member name. Use letters and digits, starting with a letter.");

                if (!Primitives.TryGetValue(type, out var primitive))
                    throw new ArgumentException($"'{type}' is not a supported value object type. Use one of: string, int, long, decimal, double, guid.");

                if (type.Equals("uint", StringComparison.OrdinalIgnoreCase))
                    log?.Invoke($"{name}: uint is not CLS-compliant, so the value object wraps an int and enforces non-negative through its rule.");

                var property = char.ToUpperInvariant(name[0]) + name[1..];

                if (fields.Any(field => field.Property == property))
                    throw new ArgumentException($"The member {property} was specified twice.");

                fields.Add(new VoField(property, primitive));
            }

            return fields;
        }

        /// <summary>
        /// A sample literal of this primitive for generated tests, and a second,
        /// different one for update paths.
        /// </summary>
        public string SampleLiteral(bool updated = false) => Primitive switch
        {
            "string" => updated ? "\"Joist\"" : "\"Beam\"",
            "decimal" => updated ? "20m" : "10m",
            "double" => updated ? "20.5" : "10.5",
            "Guid" => "Guid.NewGuid()",
            _ => updated ? "20" : "10"
        };

        /// <summary>
        /// A literal that breaks the starter invariant of this primitive.
        /// </summary>
        public string InvalidLiteral() => Primitive switch
        {
            "string" => "\" \"",
            "Guid" => "Guid.Empty",
            _ => "-1"
        };
    }
}
