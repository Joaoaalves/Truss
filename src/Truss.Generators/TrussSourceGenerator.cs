using Microsoft.CodeAnalysis;

namespace Truss.Generators
{
    /// <summary>
    /// Discovers Truss handlers and validators at compile time and generates
    /// the registration module that replaces runtime assembly scanning.
    /// Also reports diagnostics for requests without a handler and requests with more than one.
    /// </summary>
    [Generator]
    public sealed class TrussSourceGenerator : IIncrementalGenerator
    {
        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var model = context.CompilationProvider.Select(
                static (compilation, cancellationToken) => TrussModelBuilder.Build(compilation, cancellationToken));

            context.RegisterSourceOutput(model, static (productionContext, model) =>
            {
                foreach (var diagnostic in model.Diagnostics)
                    productionContext.ReportDiagnostic(diagnostic);

                if (model.Assemblies.Length > 0)
                    productionContext.AddSource("TrussModule.g.cs", TrussEmitter.Emit(model));
            });
        }
    }
}
