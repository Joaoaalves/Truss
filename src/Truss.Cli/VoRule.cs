using System.Globalization;

namespace Truss.Cli
{
    internal enum VoRuleKind
    {
        NotEmpty,
        MinLength,
        MaxLength,
        NonNegative,
        Positive,
        AtLeast,
        GreaterThan,
        AtMost,
        LessThan,
        NotDefault
    }

    /// <summary>
    /// One resolved invariant of a value object member. The rule segment of a
    /// specification (Name:string:3..120) borrows what people already type
    /// elsewhere: inclusive ranges as in SQL BETWEEN, the gt/gte/lt/lte
    /// comparators of REST filters, and min/max as synonyms in the Laravel
    /// tradition, where a bound on a string means its length. Every token is
    /// shell-safe: no quoting needed at the terminal.
    /// </summary>
    internal sealed record VoRule(VoRuleKind Kind, decimal Bound = 0)
    {
        /// <summary>
        /// Parses a + separated rule segment against the member's primitive and
        /// returns the full resolved list, defaults included: strings are never
        /// empty and carry a length ceiling (200 unless bounded), numbers carry
        /// a floor (non-negative unless another floor is given), guids are never
        /// default.
        /// </summary>
        public static List<VoRule> Resolve(string? segment, string member, string primitive)
        {
            var floors = new List<(string Token, bool Exclusive, decimal Bound)>();
            var ceilings = new List<(string Token, bool Exclusive, decimal Bound)>();
            var positive = false;
            var nonNegative = false;

            foreach (var token in Tokens(segment))
            {
                if (token.Contains("..", StringComparison.Ordinal))
                {
                    var (floor, ceiling) = ParseRange(token, member);

                    if (floor is { } low)
                        floors.Add((token, false, low));

                    if (ceiling is { } high)
                        ceilings.Add((token, false, high));

                    continue;
                }

                switch (SplitComparator(token, member))
                {
                    case ("pos", _):
                        positive = true;
                        break;
                    case ("nonneg", _):
                        nonNegative = true;
                        break;
                    case ("gt", var bound):
                        floors.Add((token, true, bound));
                        break;
                    case ("gte" or "min", var bound):
                        floors.Add((token, false, bound));
                        break;
                    case ("lt", var bound):
                        ceilings.Add((token, true, bound));
                        break;
                    case ("lte" or "max", var bound):
                        ceilings.Add((token, false, bound));
                        break;
                    default:
                        throw new ArgumentException(
                            $"{member}: '{token}' is not a known rule. Use a range like 0..900, the comparators gt=, gte=, lt=, lte= (min= and max= are synonyms), or pos.");
                }
            }

            if (floors.Count > 1 || ceilings.Count > 1 || (positive && floors.Count > 0))
                throw new ArgumentException($"{member}: give at most one floor and one ceiling.");

            return primitive switch
            {
                "string" => ResolveString(member, floors, ceilings, positive, nonNegative),
                "Guid" => ResolveGuid(member, floors, ceilings, positive, nonNegative),
                _ => ResolveNumber(primitive, member, floors, ceilings, positive, nonNegative)
            };
        }

        /// <summary>
        /// Whether a bare specification segment reads as rules rather than a
        /// type, so Name:3..120 works without spelling :string:.
        /// </summary>
        public static bool LooksLikeRules(string segment)
        {
            return segment.Contains("..", StringComparison.Ordinal)
                || segment.Contains('=')
                || segment.Contains('+')
                || segment is "pos" or "nonneg";
        }

        private static List<VoRule> ResolveString(
            string member,
            List<(string Token, bool Exclusive, decimal Bound)> floors,
            List<(string Token, bool Exclusive, decimal Bound)> ceilings,
            bool positive,
            bool nonNegative)
        {
            if (positive || nonNegative)
                throw new ArgumentException($"{member}: pos and nonneg apply to numbers; on a string, bound the length with a range or min=/max=.");

            var rules = new List<VoRule> { new(VoRuleKind.NotEmpty) };

            // On a string every bound measures length, so exclusive bounds
            // normalize to inclusive whole characters.
            if (floors.Count == 1)
            {
                var floor = floors[0];
                rules.Add(new VoRule(VoRuleKind.MinLength, WholeBound(floor, member) + (floor.Exclusive ? 1 : 0)));
            }

            var ceiling = ceilings.Count == 1
                ? WholeBound(ceilings[0], member) - (ceilings[0].Exclusive ? 1 : 0)
                : 200;

            rules.Add(new VoRule(VoRuleKind.MaxLength, ceiling));

            return rules;
        }

        private static List<VoRule> ResolveNumber(
            string primitive,
            string member,
            List<(string Token, bool Exclusive, decimal Bound)> floors,
            List<(string Token, bool Exclusive, decimal Bound)> ceilings,
            bool positive,
            bool nonNegative)
        {
            var rules = new List<VoRule>();

            if (primitive is "int" or "long")
            {
                foreach (var bound in floors.Concat(ceilings))
                    WholeBound(bound, member);
            }

            if (positive)
                rules.Add(new VoRule(VoRuleKind.Positive));
            else if (floors.Count == 1)
                rules.Add(new VoRule(floors[0].Exclusive ? VoRuleKind.GreaterThan : VoRuleKind.AtLeast, floors[0].Bound));
            else
                rules.Add(new VoRule(VoRuleKind.NonNegative));

            if (ceilings.Count == 1)
                rules.Add(new VoRule(ceilings[0].Exclusive ? VoRuleKind.LessThan : VoRuleKind.AtMost, ceilings[0].Bound));

            return rules;
        }

        private static List<VoRule> ResolveGuid(
            string member,
            List<(string Token, bool Exclusive, decimal Bound)> floors,
            List<(string Token, bool Exclusive, decimal Bound)> ceilings,
            bool positive,
            bool nonNegative)
        {
            if (floors.Count > 0 || ceilings.Count > 0 || positive || nonNegative)
                throw new ArgumentException($"{member}: guids only carry the not-default rule; write further invariants inside the value object.");

            return [new VoRule(VoRuleKind.NotDefault)];
        }

        private static IEnumerable<string> Tokens(string? segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
                yield break;

            foreach (var token in segment.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                yield return token;
        }

        private static (decimal? Floor, decimal? Ceiling) ParseRange(string token, string member)
        {
            var parts = token.Split("..", 2, StringSplitOptions.TrimEntries);

            var floor = parts[0].Length > 0 ? ParseBound(parts[0], token, member) : (decimal?)null;
            var ceiling = parts[1].Length > 0 ? ParseBound(parts[1], token, member) : (decimal?)null;

            if (floor is null && ceiling is null)
                throw new ArgumentException($"{member}: '{token}' needs at least one side, like 0..900, 1.. or ..120.");

            if (floor > ceiling)
                throw new ArgumentException($"{member}: the range '{token}' is empty.");

            return (floor, ceiling);
        }

        private static (string Kind, decimal Bound) SplitComparator(string token, string member)
        {
            var parts = token.Split('=', 2, StringSplitOptions.TrimEntries);

            if (parts.Length == 1)
                return (parts[0].ToLowerInvariant(), 0);

            return (parts[0].ToLowerInvariant(), ParseBound(parts[1], token, member));
        }

        private static decimal ParseBound(string text, string token, string member)
        {
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var bound))
                throw new ArgumentException($"{member}: '{token}' needs a numeric bound.");

            return bound;
        }

        private static decimal WholeBound((string Token, bool Exclusive, decimal Bound) bound, string member)
        {
            if (bound.Bound != decimal.Truncate(bound.Bound))
                throw new ArgumentException($"{member}: the bound in '{bound.Token}' must be a whole number here.");

            return bound.Bound;
        }
    }
}
