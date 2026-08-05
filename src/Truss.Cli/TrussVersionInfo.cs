using System.Reflection;

namespace Truss.Cli
{
    internal static class TrussVersionInfo
    {
        public static string Current()
        {
            var informational = typeof(TrussVersionInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (string.IsNullOrEmpty(informational))
                return "*-*";

            var metadataStart = informational.IndexOf('+');
            var version = metadataStart > 0 ? informational[..metadataStart] : informational;

            return version.StartsWith("0.0.0") ? "*-*" : version;
        }
    }
}
