using System.Diagnostics;
using Xunit;

namespace Truss.Cli.Tests
{
    public class ScaffoldBuildTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();
        private readonly string _feed;
        private readonly string _packagesCache;

        public ScaffoldBuildTests()
        {
            _feed = Path.Combine(_workspace.Directory, "feed");
            _packagesCache = Path.Combine(_workspace.Directory, "nuget-cache");
        }

        [Fact]
        public void ScaffoldedProject_Builds_WithAllModulesAdded()
        {
            PackFramework();

            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--local-packages", _feed));
            var root = _workspace.Root("Shop");

            AssertBuildSucceeds(root);

            Assert.Equal(0, _workspace.Run("add", "messaging", "--transport", "inmemory", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "jobs", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "observability", "--dashboard", "aspire", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "mapping", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "command", "ArchiveProduct", "--context", "Catalog", "--project", root));
            Assert.Equal(0, _workspace.Run("doctor", "--project", root));

            AssertBuildSucceeds(root);

            Assert.Equal(0, _workspace.Scaffold("IdShop", "sqlite", "--local-packages", _feed));
            var identityRoot = _workspace.Root("IdShop");

            Assert.Equal(0, _workspace.Run("add", "auth", "--provider", "identity", "--project", identityRoot));

            AssertBuildSucceeds(identityRoot, "IdShop");
        }

        private void PackFramework()
        {
            var repoRoot = FindRepoRoot();
            var result = RunProcess(repoRoot, "dotnet", $"pack Truss.slnx -c Release -o {_feed} --nologo", isolateNuGetCache: false);

            Assert.True(result.ExitCode == 0, $"dotnet pack failed:{Environment.NewLine}{result.Output}");
        }

        private void AssertBuildSucceeds(string root, string name = "Shop")
        {
            var result = RunProcess(root, "dotnet", $"build {name}.slnx -c Release --nologo", isolateNuGetCache: true);

            Assert.True(result.ExitCode == 0, $"Scaffolded project failed to build:{Environment.NewLine}{result.Output}");
        }

        private (int ExitCode, string Output) RunProcess(string workingDirectory, string fileName, string arguments, bool isolateNuGetCache)
        {
            var startInfo = new ProcessStartInfo(fileName, arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (isolateNuGetCache)
                startInfo.Environment["NUGET_PACKAGES"] = _packagesCache;

            startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";

            using var process = Process.Start(startInfo)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output);
        }

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Truss.slnx")))
                directory = directory.Parent!;

            Assert.NotNull(directory);
            return directory.FullName;
        }

        public void Dispose() => _workspace.Dispose();
    }
}
