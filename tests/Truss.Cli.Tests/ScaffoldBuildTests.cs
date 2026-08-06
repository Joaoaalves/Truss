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
            Assert.Equal(0, _workspace.Run("add", "email", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "command", "ArchiveProduct", "--context", "Catalog", "--project", root));
            Assert.Equal(0, _workspace.Run("doctor", "--project", root));

            AssertBuildSucceeds(root);

            RunMigrations(root);

            Assert.Equal(0, _workspace.Scaffold("IdShop", "sqlite", "--local-packages", _feed));
            var identityRoot = _workspace.Root("IdShop");

            Assert.Equal(0, _workspace.Run("add", "email", "--provider", "resend", "--project", identityRoot));
            Assert.Equal(0, _workspace.Run("add", "auth", "--provider", "identity", "--project", identityRoot));

            AssertBuildSucceeds(identityRoot, "IdShop");
        }

        private void RunMigrations(string root)
        {
            // The db commands spawn dotnet processes from the test process, so the
            // isolation the scaffold builds get through RunProcess is applied here
            // through ambient environment variables instead. DOTNET_CLI_HOME must
            // rotate together with NUGET_PACKAGES: the sdk's tool resolver cache in
            // the cli home records where a local tool's package lives, and a stale
            // entry pointing into a deleted temp cache makes dotnet ef fail right
            // after a successful dotnet tool restore.
            Environment.SetEnvironmentVariable("NUGET_PACKAGES", _packagesCache);
            Environment.SetEnvironmentVariable("DOTNET_CLI_HOME", Path.Combine(_workspace.Directory, "cli-home"));
            Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

            try
            {
                Assert.Equal(0, _workspace.Run("db", "add", "InitialCreate", "--project", root));

                var migrations = Path.Combine(root, "src", "Shop.Infrastructure", "Migrations");
                Assert.True(Directory.Exists(migrations) && Directory.EnumerateFiles(migrations).Any(file => file.Contains("InitialCreate")));

                Assert.Equal(0, _workspace.Run("db", "migrate", "--project", root));
            }
            finally
            {
                Environment.SetEnvironmentVariable("NUGET_PACKAGES", null);
                Environment.SetEnvironmentVariable("DOTNET_CLI_HOME", null);
                Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", null);
            }
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
