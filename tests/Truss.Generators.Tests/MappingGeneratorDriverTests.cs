using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Truss.Mapping.Tests
{
    public class MappingGeneratorDriverTests
    {
        private static CSharpCompilation CreateCompilation(string source)
        {
            var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Append(typeof(Truss.Domain.IBusinessRule).Assembly.Location)
                .Distinct();

            var references = paths
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
                .ToList();

            return CSharpCompilation.Create(
                "MappingTestApp",
                [CSharpSyntaxTree.ParseText(source)],
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static (GeneratorDriverRunResult Result, Compilation Output) RunGenerator(string source)
        {
            var compilation = CreateCompilation(source);
            var driver = CSharpGeneratorDriver.Create(new MappingGenerator());

            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
                compilation, out var output, out _);

            return (driver.GetRunResult(), output);
        }

        private const string RecordMapper = """
            using System;
            using Truss.Mapping;

            namespace TestApp
            {
                public sealed record Customer(Guid Id, string Name, int Age);

                public sealed record CustomerDto(Guid Id, string Name);

                [Mapper]
                public static partial class Mappings
                {
                    public static partial CustomerDto ToDto(Customer customer);
                }
            }
            """;

        [Fact]
        public void Generator_ImplementsPartialMethod_AndOutputCompiles()
        {
            var (result, output) = RunGenerator(RecordMapper);

            var generated = result.Results[0].GeneratedSources.Single(s => s.HintName == "Mappings.Mappings.g.cs");
            Assert.Contains("public static partial global::TestApp.CustomerDto ToDto", generated.SourceText.ToString());

            var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(errors);
        }

        [Fact]
        public void Generator_UnwrapsTypedIds()
        {
            var source = """
                using System;
                using Truss.Domain;
                using Truss.Mapping;

                namespace TestApp
                {
                    public sealed record CustomerId(Guid Value) : TypedId<Guid>(Value);

                    public sealed record Customer(CustomerId Id, string Name);

                    public sealed record CustomerDto(Guid Id, string Name);

                    [Mapper]
                    public static partial class Mappings
                    {
                        public static partial CustomerDto ToDto(Customer customer);
                    }
                }
                """;

            var (result, output) = RunGenerator(source);

            var generated = result.Results[0].GeneratedSources.Single(s => s.HintName == "Mappings.Mappings.g.cs");
            Assert.Contains("customer.Id.Value", generated.SourceText.ToString());
            Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        }

        [Fact]
        public void Generator_ReportsUnmappedMember()
        {
            var source = """
                using System;
                using Truss.Mapping;

                namespace TestApp
                {
                    public sealed record Customer(Guid Id);

                    public sealed record CustomerDto(Guid Id, string Name);

                    [Mapper]
                    public static partial class Mappings
                    {
                        public static partial CustomerDto ToDto(Customer customer);
                    }
                }
                """;

            var (result, _) = RunGenerator(source);

            var diagnostic = Assert.Single(result.Diagnostics, d => d.Id == "TRUSSMAP001");
            Assert.Contains("Name", diagnostic.GetMessage());
        }

        [Fact]
        public void Generator_ReportsNonPartialClass()
        {
            var source = """
                using System;
                using Truss.Mapping;

                namespace TestApp
                {
                    public sealed record Customer(Guid Id);

                    public sealed record CustomerDto(Guid Id);

                    [Mapper]
                    public static class Mappings
                    {
                    }
                }
                """;

            var (result, _) = RunGenerator(source);

            Assert.Single(result.Diagnostics, d => d.Id == "TRUSSMAP002");
        }
    }
}
