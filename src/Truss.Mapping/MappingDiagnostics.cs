using Microsoft.CodeAnalysis;

namespace Truss.Mapping
{
    internal static class MappingDiagnostics
    {
        public static readonly DiagnosticDescriptor UnmappedMember = new(
            id: "TRUSSMAP001",
            title: "Target member cannot be mapped",
            messageFormat: "Cannot map '{0}' of {1}: no matching source member or supported conversion was found",
            category: "Truss.Mapping",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor InvalidMapper = new(
            id: "TRUSSMAP002",
            title: "Invalid mapper declaration",
            messageFormat: "{0}",
            category: "Truss.Mapping",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
