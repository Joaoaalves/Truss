using Microsoft.CodeAnalysis;

namespace Truss.Generators
{
    internal static class TrussDiagnostics
    {
        public static readonly DiagnosticDescriptor MissingHandler = new(
            id: "TRUSS001",
            title: "Request has no handler",
            messageFormat: "No handler was found for request type '{0}'",
            category: "Truss",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateHandler = new(
            id: "TRUSS002",
            title: "Request has multiple handlers",
            messageFormat: "Request type '{0}' has more than one handler: {1}",
            category: "Truss",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InaccessibleImplementations = new(
            id: "TRUSS003",
            title: "Implementations not accessible to generated code",
            messageFormat: "Assembly '{0}' contains Truss implementations that are not accessible from the generated registration; runtime scanning will be used for this assembly",
            category: "Truss",
            defaultSeverity: DiagnosticSeverity.Info,
            isEnabledByDefault: true);
    }
}
