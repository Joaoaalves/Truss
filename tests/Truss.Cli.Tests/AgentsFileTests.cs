using Xunit;

namespace Truss.Cli.Tests
{
    public class AgentsFileTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        [Fact]
        public void Scaffold_WritesAgentsFile()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));

            var agents = _workspace.ReadFile("Shop", "AGENTS.md");
            Assert.StartsWith("# Shop", agents);
            Assert.Contains("src/Shop.Domain", agents);
            Assert.Contains("truss generate aggregate", agents);
            Assert.Contains("Database: sqlite", agents);
            Assert.Contains("llms.txt", agents);
            Assert.DoesNotContain("Messaging", agents);
        }

        [Fact]
        public void AddModule_RegeneratesManagedBlock_AndKeepsUserContent()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            var path = Path.Combine(root, "AGENTS.md");
            File.AppendAllText(path, "\n## Team notes\n\nAlways run the importer locally first.\n");

            Assert.Equal(0, _workspace.Run("add", "messaging", "--transport", "redis", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "jobs", "--project", root));

            var agents = _workspace.ReadFile("Shop", "AGENTS.md");
            Assert.Contains("Messaging (redis transport)", agents);
            Assert.Contains("IJobScheduler.Enqueue", agents);
            Assert.Contains("Always run the importer locally first.", agents);
            Assert.Equal(1, agents.Split("## Workflow").Length - 1);
        }

        [Fact]
        public void AgentsFile_WithoutMarkers_IsLeftAlone()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            var path = Path.Combine(root, "AGENTS.md");
            File.WriteAllText(path, "# Mine now\n");

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));

            Assert.Equal("# Mine now\n", _workspace.ReadFile("Shop", "AGENTS.md"));
        }

        public void Dispose() => _workspace.Dispose();
    }
}
