using Xunit;

namespace Truss.Cli.Tests
{
    /// <summary>
    /// The scaffold leaves comment markers in the composition roots and every
    /// insertion targets them first, so truss add keeps working after the user
    /// reformats the lines the old anchors depended on.
    /// </summary>
    public class MarkerTests : IDisposable
    {
        private readonly CliTestWorkspace _workspace = new();

        [Fact]
        public void NewProject_CarriesTheMarkers()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("// truss: services", program);
            Assert.Contains("// truss: middleware", program);
            Assert.Contains("// truss: endpoints", program);

            var context = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.Contains("// truss: model", context);
        }

        [Fact]
        public void AddModule_SurvivesAReformattedProgram()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            var programPath = Path.Combine(root, "src", "Shop.Api", "Program.cs");
            var program = File.ReadAllText(programPath);

            File.WriteAllText(programPath, program
                .Replace("var app = builder.Build();", "var app =\n    builder.Build();")
                .Replace("app.Run();", "app\n    .Run();"));

            Assert.Equal(0, _workspace.Run("add", "messaging", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "jobs", "--project", root));

            var updated = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            Assert.Contains("AddTrussMessaging", updated);
            Assert.Contains("app.MapTrussJobs();", updated);

            var context = _workspace.ReadFile("Shop", "src", "Shop.Infrastructure", "AppDbContext.cs");
            Assert.Contains("ApplyTrussOutbox", context);
        }

        [Fact]
        public void InstallOrder_AccumulatesAboveTheMarker()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "tenancy", "--project", root));

            var program = _workspace.ReadFile("Shop", "src", "Shop.Api", "Program.cs");
            var authentication = program.IndexOf("app.UseAuthentication();", StringComparison.Ordinal);
            var tenancy = program.IndexOf("app.UseTrussTenancy();", StringComparison.Ordinal);
            var marker = program.IndexOf("// truss: middleware", StringComparison.Ordinal);

            Assert.True(authentication >= 0 && tenancy >= 0);
            Assert.True(authentication < tenancy, "authentication must run before tenant resolution");
            Assert.True(tenancy < marker, "middleware accumulates above its marker");
        }

        [Fact]
        public void AddModule_WithoutTheMarker_PrintsWhatToPasteInsteadOfGuessing()
        {
            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite"));
            var root = _workspace.Root("Shop");

            var programPath = Path.Combine(root, "src", "Shop.Api", "Program.cs");
            Strip(programPath);

            var output = _workspace.Capture("add", "messaging", "--project", root);

            Assert.Contains("// truss: services", output);
            Assert.Contains("AddTrussMessaging", output);
            Assert.DoesNotContain("AddTrussMessaging", File.ReadAllText(programPath));
        }

        private static void Strip(string path)
        {
            var lines = File.ReadAllLines(path).Where(line => !line.TrimStart().StartsWith("// truss:", StringComparison.Ordinal));
            File.WriteAllLines(path, lines);
        }

        public void Dispose()
        {
            _workspace.Dispose();
        }
    }
}
