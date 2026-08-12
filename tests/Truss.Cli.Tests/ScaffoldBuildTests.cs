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

            Assert.Equal(0, _workspace.Scaffold("Shop", "sqlite", "--sample", "--local-packages", _feed));
            var root = _workspace.Root("Shop");

            AssertBuildSucceeds(root);

            Assert.Equal(0, _workspace.Run("add", "messaging", "--transport", "inmemory", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "jobs", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "observability", "--dashboard", "aspire", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "mapping", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "email", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Invoice", "--context", "Billing", "--crud", "--vo", "Name:string:2..120", "--vo", "Amount:decimal:pos", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "entity", "InvoiceLine", "--context", "Billing", "--aggregate", "Invoice", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--bind-user", "Invoice", "--external", "google,github", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "tenancy", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "rbac", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "worker", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "command", "ArchiveProduct", "--context", "Catalog", "--project", root));

            // The extraction path: a context created in its own projects gets a
            // crud slice, and a folder context with a slice moves into projects
            // intact; both must compile and pass with the rest. The context that
            // holds the account-bound aggregate is refused.
            Assert.Equal(0, _workspace.Run("generate", "context", "Sales", "--as-projects", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Order", "--context", "Sales", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Shipment", "--context", "Shipping", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "context", "Shipping", "--as-projects", "--project", root));
            Assert.Equal(1, _workspace.Run("generate", "context", "Billing", "--as-projects", "--project", root));

            // The v1.0 promise: splitting the context into its own service with
            // its own database is mechanical. The handlers do not change, the
            // service hosts the moved routes, and the whole solution still
            // builds and passes, including the context's tests on the service's
            // DbContext.
            Assert.Equal(0, _workspace.Run("split", "Shipping", "--project", root));

            // A generated slice must unwind completely: the project has to build
            // after its context is removed.
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Coupon", "--context", "Promo", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("remove", "context", "Promo", "--project", root));

            Assert.Equal(0, _workspace.Run("doctor", "--project", root));

            // Running the scaffolded test suite proves the generated tests are
            // green, not merely compiling: the sample tests, the Invoice crud
            // slice through the pipeline and the host smoke test.
            AssertTestsSucceed(root);

            RunMigrations(root);

            Assert.Equal(0, _workspace.Scaffold("IdShop", "sqlite", "--local-packages", _feed));
            var identityRoot = _workspace.Root("IdShop");

            Assert.Equal(0, _workspace.Run("add", "email", "--provider", "resend", "--project", identityRoot));

            // The merge binding makes an existing aggregate the account itself, so
            // give the generated one the identity fields merge mode requires.
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Member", "--context", "Community", "--project", identityRoot));
            File.WriteAllText(
                Path.Combine(identityRoot, "src", "IdShop.Domain", "Community", "Member", "Member.cs"),
                MergedMemberAggregate);
            File.WriteAllText(
                Path.Combine(identityRoot, "tests", "IdShop.Domain.Tests", "Community", "MemberTests.cs"),
                MergedMemberTests);

            Assert.Equal(0, _workspace.Run(
                "add", "auth", "--provider", "identity",
                "--bind-user", "Member", "--bind-mode", "merge",
                "--external", "microsoft", "--project", identityRoot));

            AssertBuildSucceeds(identityRoot, "IdShop");
        }

        private const string MergedMemberAggregate = """
            using IdShop.Domain.Community.Member.Events;
            using IdShop.Domain.Community.Member.ValueObjects;
            using Truss.Domain;

            namespace IdShop.Domain.Community.Member
            {
                public class Member : AggregateRoot<MemberId>
                {
                    private Member()
                    {
                    }

                    private Member(MemberId id) : base(id)
                    {
                    }

                    public string Email { get; private set; } = string.Empty;

                    public string Name { get; private set; } = string.Empty;

                    public static Member Register(string email, string name)
                    {
                        var member = new Member(new MemberId(Guid.NewGuid()))
                        {
                            Email = email.ToLowerInvariant(),
                            Name = name
                        };

                        member.AddDomainEvent(new MemberCreated(member.Id));
                        return member;
                    }
                }
            }
            """;

        private const string MergedMemberTests = """
            using IdShop.Domain.Community.Member;
            using IdShop.Domain.Community.Member.Events;
            using Xunit;

            namespace IdShop.Domain.Tests.Community
            {
                public class MemberTests
                {
                    [Fact]
                    public void Register_NormalizesTheEmail_AndRaisesTheCreationEvent()
                    {
                        var member = Member.Register("Ana@Example.com", "Ana");

                        Assert.Equal("ana@example.com", member.Email);
                        Assert.Contains(member.DomainEvents, domainEvent => domainEvent is MemberCreated);
                    }
                }
            }
            """;

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

        private void AssertTestsSucceed(string root, string name = "Shop")
        {
            var result = RunProcess(root, "dotnet", $"test {name}.slnx -c Release --nologo", isolateNuGetCache: true);

            Assert.True(result.ExitCode == 0, $"Scaffolded tests failed:{Environment.NewLine}{result.Output}");
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
