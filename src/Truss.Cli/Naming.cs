using System.Text.RegularExpressions;

namespace Truss.Cli
{
    internal static partial class Naming
    {
        [GeneratedRegex("^[A-Za-z][A-Za-z0-9._]*[A-Za-z0-9]$|^[A-Za-z]$")]
        private static partial Regex ProjectName();

        [GeneratedRegex("^[A-Za-z][A-Za-z0-9]*$")]
        private static partial Regex TypeName();

        public static bool IsValidProjectName(string name) => ProjectName().IsMatch(name);

        public static bool IsValidTypeName(string name) => TypeName().IsMatch(name);
    }
}
