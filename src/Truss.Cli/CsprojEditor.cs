namespace Truss.Cli
{
    internal static class CsprojEditor
    {
        public static bool AddProjectReference(string csprojPath, string includePath)
        {
            var content = File.ReadAllText(csprojPath);

            if (content.Contains($"Include=\"{includePath}\""))
                return false;

            var reference = $"    <ProjectReference Include=\"{includePath}\" />";

            var marker = "</Project>";
            var itemGroup = $"  <ItemGroup>{Environment.NewLine}{reference}{Environment.NewLine}  </ItemGroup>{Environment.NewLine}{Environment.NewLine}{marker}";

            File.WriteAllText(csprojPath, content.Replace(marker, itemGroup));
            return true;
        }

        public static bool AddPackageReference(string csprojPath, string packageId, string version, bool developmentDependency = false)
        {
            var content = File.ReadAllText(csprojPath);

            if (content.Contains($"Include=\"{packageId}\""))
                return false;

            var reference = developmentDependency
                ? $"    <PackageReference Include=\"{packageId}\" Version=\"{version}\" PrivateAssets=\"all\" />"
                : $"    <PackageReference Include=\"{packageId}\" Version=\"{version}\" />";

            var marker = "</Project>";
            var itemGroup = $"  <ItemGroup>{Environment.NewLine}{reference}{Environment.NewLine}  </ItemGroup>{Environment.NewLine}{Environment.NewLine}{marker}";

            File.WriteAllText(csprojPath, content.Replace(marker, itemGroup));
            return true;
        }
    }
}
