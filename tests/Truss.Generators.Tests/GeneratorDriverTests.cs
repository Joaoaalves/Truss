using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Truss.Generators.Tests
{
    public class GeneratorDriverTests
    {
        private static CSharpCompilation CreateCompilation(string source)
        {
            var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Append(typeof(Truss.Domain.IBusinessRule).Assembly.Location)
                .Append(typeof(Truss.Application.ICommand<>).Assembly.Location)
                .Append(typeof(Truss.Application.Dispatcher).Assembly.Location)
                .Append(typeof(FluentValidation.IValidator<>).Assembly.Location)
                .Append(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location)
                .Distinct();

            var references = paths
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();

            return CSharpCompilation.Create(
                "TestApp",
                [CSharpSyntaxTree.ParseText(source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static (GeneratorDriverRunResult Result, Compilation Output) RunGenerator(string source)
        {
            var compilation = CreateCompilation(source);
            var driver = CSharpGeneratorDriver.Create(new TrussSourceGenerator());

            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
                compilation, out var output, out _);

            return (driver.GetRunResult(), output);
        }

        private const string CommandWithHandler = """
            using Truss.Application;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestApp
            {
                public sealed record Ping(string Value) : ICommand<string>;

                public sealed class PingHandler : ICommandHandler<Ping, string>
                {
                    public Task<string> Handle(Ping request, CancellationToken cancellationToken)
                        => Task.FromResult(request.Value);
                }
            }
            """;

        [Fact]
        public void Generator_EmitsRegistrationAndPrime()
        {
            var (result, _) = RunGenerator(CommandWithHandler);

            var generated = Assert.Single(result.Results[0].GeneratedSources).SourceText.ToString();

            Assert.Contains("RegisterAssembly(typeof(global::TestApp.PingHandler).Assembly", generated);
            Assert.Contains("AddTransient<global::Truss.Application.IRequestHandler<global::TestApp.Ping, string>, global::TestApp.PingHandler>", generated);
            Assert.Contains("PrimeRequest<global::TestApp.Ping, string>", generated);
        }

        [Fact]
        public void Generator_Output_CompilesWithoutErrors()
        {
            var (_, output) = RunGenerator(CommandWithHandler);

            var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            Assert.Empty(errors);
        }

        [Fact]
        public void Generator_ReportsMissingHandler()
        {
            var source = """
                using Truss.Application;

                namespace TestApp
                {
                    public sealed record Orphan(string Value) : ICommand<string>;
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "TRUSS001");
            Assert.Contains("TestApp.Orphan", diagnostic.GetMessage());
        }

        [Fact]
        public void Generator_ReportsDuplicateHandlers()
        {
            var source = """
                using Truss.Application;
                using System.Threading;
                using System.Threading.Tasks;

                namespace TestApp
                {
                    public sealed record Ping(string Value) : ICommand<string>;

                    public sealed class FirstHandler : ICommandHandler<Ping, string>
                    {
                        public Task<string> Handle(Ping request, CancellationToken cancellationToken)
                            => Task.FromResult("first");
                    }

                    public sealed class SecondHandler : ICommandHandler<Ping, string>
                    {
                        public Task<string> Handle(Ping request, CancellationToken cancellationToken)
                            => Task.FromResult("second");
                    }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "TRUSS002");
            Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
            Assert.Contains("FirstHandler", diagnostic.GetMessage());
            Assert.Contains("SecondHandler", diagnostic.GetMessage());
        }
    }
}
