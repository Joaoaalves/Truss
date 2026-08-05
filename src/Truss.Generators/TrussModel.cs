using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Truss.Generators
{
    internal sealed record ServiceRegistration(string ServiceType, string ImplementationType);

    internal sealed record RequestPrime(string RequestType, string ResponseType);

    internal sealed record AssemblyModel(
        string AssemblyName,
        string AnchorType,
        ImmutableArray<ServiceRegistration> Services,
        ImmutableArray<RequestPrime> RequestPrimes,
        ImmutableArray<string> DomainEventPrimes);

    internal sealed record TrussModel(
        ImmutableArray<AssemblyModel> Assemblies,
        ImmutableArray<Diagnostic> Diagnostics)
    {
        public static readonly TrussModel Empty = new(
            ImmutableArray<AssemblyModel>.Empty,
            ImmutableArray<Diagnostic>.Empty);
    }
}
