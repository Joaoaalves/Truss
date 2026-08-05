using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Truss.Mapping
{
    internal sealed record MapperModel(
        string? HintName,
        string? Source,
        ImmutableArray<Diagnostic> Diagnostics);
}
