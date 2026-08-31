using System.Diagnostics;
using System.Net.Http.Json;
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
            Assert.Equal(0, _workspace.Run("add", "email", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Invoice", "--context", "Billing", "--crud", "--vo", "Name:string:2..120", "--vo", "Amount:decimal:pos", "--project", root));
            Assert.Equal(0, _workspace.Run("generate", "entity", "InvoiceLine", "--context", "Billing", "--aggregate", "Invoice", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "auth", "--bind-user", "Invoice", "--external", "google,github", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "tenancy", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "rbac", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "idempotency", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "support", "--project", root));
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

            // The worker must not merely compile: in Development the host
            // validates the container at boot, and a module whose services were
            // wired only into the API dies right here. The account slice
            // regression lived exactly in this gap.
            AssertWorkerBoots(Path.Combine(root, "src", "Shop.Worker"));

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

            // Support renders against the account's id; under a merge binding
            // that is the bound aggregate's id, not a scaffolded UserId.
            Assert.Equal(0, _workspace.Run("add", "support", "--project", identityRoot));

            AssertBuildSucceeds(identityRoot, "IdShop");
        }


        /// <summary>
        /// The 0.6 promise, application side: a scaffolded app in deck mode
        /// goes live and holds a whole support conversation it never stored,
        /// over real HTTP against a contract-faithful deck: the credential
        /// travels, writes carry idempotency keys, the requester is the
        /// signed-in account, and the agent's reply comes back to the user.
        /// </summary>
        [Fact]
        public async Task DeckMode_HoldsAConversation_OverRealHttp()
        {
            PackFramework();

            Assert.Equal(0, _workspace.Scaffold("Hub", "sqlite", "--local-packages", _feed));
            var root = _workspace.Root("Hub");

            Assert.Equal(0, _workspace.Run("add", "auth", "--project", root));
            Assert.Equal(0, _workspace.Run("add", "support", "--deck", "http://localhost:5301", "--project", root));

            AssertBuildSucceeds(root, "Hub");

            using var deck = new StubDeck(5301, "deck_e2e");

            var (app, output) = StartHost(
                Path.Combine(root, "src", "Hub.Api"),
                "http://localhost:5302",
                new Dictionary<string, string> { ["Truss__Support__Deck__ApiKey"] = "deck_e2e" });

            try
            {
                using var client = new HttpClient { BaseAddress = new Uri("http://localhost:5302") };
                await WaitForHealth(client, "http://localhost:5302/health", output);

                var registered = await client.PostAsJsonAsync("/auth/register",
                    new { email = "maria@example.com", name = "Maria", password = "Str0ng!Passw0rd" });
                var userId = await registered.Content.ReadFromJsonAsync<Guid>();

                var login = await client.PostAsJsonAsync("/auth/login",
                    new { email = "maria@example.com", password = "Str0ng!Passw0rd" });
                using var tokens = System.Text.Json.JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Bearer", tokens.RootElement.GetProperty("accessToken").GetString());

                var opened = await client.PostAsJsonAsync("/support/tickets",
                    new { subject = "The export is broken", body = "It fails with a 500." });
                Assert.Equal(System.Net.HttpStatusCode.Created, opened.StatusCode);
                var ticketId = await opened.Content.ReadFromJsonAsync<Guid>();

                // The wire carried what the contract demands.
                Assert.Equal(userId.ToString(), deck.LastRequesterExternalUserId);
                Assert.All(deck.PresentedKeys, key => Assert.Equal("deck_e2e", key));
                Assert.All(deck.IdempotencyKeys, key => Assert.False(string.IsNullOrEmpty(key)));

                // The application shows a conversation it never stored.
                using var mine = System.Text.Json.JsonDocument.Parse(
                    await client.GetStringAsync("/support/tickets"));
                var summary = Assert.Single(mine.RootElement.GetProperty("items").EnumerateArray().ToList());
                Assert.Equal("WaitingOnCustomer", summary.GetProperty("status").GetString());

                using var detail = System.Text.Json.JsonDocument.Parse(
                    await client.GetStringAsync($"/support/tickets/{ticketId}"));
                var messages = detail.RootElement.GetProperty("messages").EnumerateArray().ToList();
                Assert.Equal(2, messages.Count);
                Assert.Contains(messages, message => message.GetProperty("body").GetString() == "We are on it.");
            }
            finally
            {
                Kill(app);
            }
        }

        /// <summary>
        /// The proof of the v1.0 promise: a scaffolded project is split, both
        /// processes go live, and a query dispatched in the monolith is
        /// answered by the handler now running in the service. The handler and
        /// the calling code are ordinary Truss code; only the composition
        /// roots know a network sits between them.
        /// </summary>
        [Fact]
        public async Task SplitService_AnswersARemoteQuery_AcrossTwoLiveProcesses()
        {
            PackFramework();

            Assert.Equal(0, _workspace.Scaffold("Duo", "sqlite", "--local-packages", _feed));
            var root = _workspace.Root("Duo");

            Assert.Equal(0, _workspace.Run("generate", "aggregate", "Shipment", "--context", "Shipping", "--crud", "--project", root));
            Assert.Equal(0, _workspace.Run("split", "Shipping", "--project", root));

            File.WriteAllText(
                Path.Combine(root, "src", "Duo.Shipping.Contracts", "GetShipmentGreeting.cs"),
                ContractQuery + Environment.NewLine);

            Directory.CreateDirectory(Path.Combine(root, "src", "Duo.Shipping.Application", "Greetings"));
            File.WriteAllText(
                Path.Combine(root, "src", "Duo.Shipping.Application", "Greetings", "GetShipmentGreetingHandler.cs"),
                ContractQueryHandler + Environment.NewLine);

            var version = TrussManifest.Load(root)!.TrussVersion;
            var apiCsproj = Path.Combine(root, "src", "Duo.Api", "Duo.Api.csproj");
            CsprojEditor.AddPackageReference(apiCsproj, "Truss.Remote", version);
            CsprojEditor.AddProjectReference(apiCsproj, "..\\Duo.Shipping.Contracts\\Duo.Shipping.Contracts.csproj");

            var programPath = Path.Combine(root, "src", "Duo.Api", "Program.cs");
            var program = File.ReadAllText(programPath)
                .Replace("using Duo.Application;", "using Duo.Application;\nusing Duo.Shipping.Contracts;")
                .Replace("// truss: services",
                    "builder.Services.AddRemoteContext<ShippingContracts>(\"Shipping\", new Uri(\"http://localhost:5240\"));\n\n// truss: services")
                .Replace("// truss: endpoints",
                    "app.MapQuery<GetShipmentGreeting, string?>(\"/shipping-greetings/{id:guid}\");\n\n// truss: endpoints");
            File.WriteAllText(programPath, program);

            AssertBuildSucceeds(root, "Duo");

            var (service, serviceOutput) = StartHost(Path.Combine(root, "src", "Duo.Shipping.Api"), "http://localhost:5240");
            var (monolith, monolithOutput) = StartHost(Path.Combine(root, "src", "Duo.Api"), "http://localhost:5241");

            try
            {
                using var client = new HttpClient();

                await WaitForHealth(client, "http://localhost:5240/health", serviceOutput);
                await WaitForHealth(client, "http://localhost:5241/health", monolithOutput);

                var id = Guid.NewGuid();
                var answer = await client.GetStringAsync($"http://localhost:5241/shipping-greetings/{id}");

                Assert.Contains($"shipment:{id}", answer);
            }
            finally
            {
                Kill(service);
                Kill(monolith);
            }
        }

        private const string ContractQuery = """
            using Truss.Application;

            namespace Duo.Shipping.Contracts
            {
                public sealed record GetShipmentGreeting(Guid Id) : IQuery<string?>;
            }
            """;

        private const string ContractQueryHandler = """
            using Duo.Shipping.Contracts;
            using Truss.Application;

            namespace Duo.Application.Shipping.Greetings
            {
                public class GetShipmentGreetingHandler : IQueryHandler<GetShipmentGreeting, string?>
                {
                    public Task<string?> Handle(GetShipmentGreeting request, CancellationToken cancellationToken)
                    {
                        return Task.FromResult<string?>($"shipment:{request.Id}");
                    }
                }
            }
            """;

        private (Process Process, System.Text.StringBuilder Output) StartHost(string projectDirectory, string url, IReadOnlyDictionary<string, string>? environment = null)
        {
            var start = new ProcessStartInfo("dotnet", "run -c Release --no-build --no-launch-profile")
            {
                WorkingDirectory = projectDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            start.Environment["ASPNETCORE_URLS"] = url;
            start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            start.Environment["DOTNET_ENVIRONMENT"] = "Development";
            start.Environment["NUGET_PACKAGES"] = _packagesCache;
            start.Environment["MSBUILDDISABLENODEREUSE"] = "1";

            foreach (var pair in environment ?? new Dictionary<string, string>())
                start.Environment[pair.Key] = pair.Value;

            var output = new System.Text.StringBuilder();
            var process = Process.Start(start)!;
            process.OutputDataReceived += (_, args) => { if (args.Data is not null) lock (output) output.AppendLine(args.Data); };
            process.ErrorDataReceived += (_, args) => { if (args.Data is not null) lock (output) output.AppendLine(args.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return (process, output);
        }

        private static async Task WaitForHealth(HttpClient client, string url, System.Text.StringBuilder output)
        {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            Exception? last = null;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if ((await client.GetAsync(url)).IsSuccessStatusCode)
                        return;
                }
                catch (HttpRequestException exception)
                {
                    last = exception;
                }

                await Task.Delay(500);
            }

            string captured;

            lock (output)
            {
                captured = output.ToString();
            }

            Assert.Fail($"The host at {url} never became healthy. {last?.Message}{Environment.NewLine}{captured}");
        }


        private void AssertWorkerBoots(string projectDirectory)
        {
            // dotnet test built the solution, but not necessarily this host's
            // apphost; build it explicitly so run --no-build has its binary.
            var built = RunProcess(projectDirectory, "dotnet", "build -c Release --nologo", isolateNuGetCache: true);
            Assert.True(built.ExitCode == 0, $"The worker did not build:{Environment.NewLine}{built.Output}");

            var (process, output) = StartHost(projectDirectory, "http://localhost:5299");

            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(90);

                while (DateTime.UtcNow < deadline)
                {
                    string captured;

                    lock (output)
                    {
                        captured = output.ToString();
                    }

                    if (captured.Contains("Application started"))
                        return;

                    if (process.HasExited)
                        Assert.Fail($"The worker exited during boot:{Environment.NewLine}{captured}");

                    Thread.Sleep(500);
                }

                string final;

                lock (output)
                {
                    final = output.ToString();
                }

                Assert.Fail($"The worker never reported started:{Environment.NewLine}{final}");
            }
            finally
            {
                Kill(process);
            }
        }

        private static void Kill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.Dispose();
            }
            catch (InvalidOperationException)
            {
            }
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
