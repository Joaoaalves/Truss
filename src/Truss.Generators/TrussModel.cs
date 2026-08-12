using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Truss.Generators
{
    internal sealed record ServiceRegistration(string ServiceType, string ImplementationType);

    internal sealed record RequestPrime(string RequestType, string ResponseType);

    internal sealed record JobRegistration(string JobType, string ArgsType);

    internal sealed record AssemblyModel(
        string AssemblyName,
        string AnchorType,
        ImmutableArray<ServiceRegistration> Services,
        ImmutableArray<RequestPrime> RequestPrimes,
        ImmutableArray<string> DomainEventPrimes,
        ImmutableArray<ServiceRegistration> MessagingHandlers,
        ImmutableArray<string> IntegrationEvents,
        ImmutableArray<JobRegistration> Jobs);

    internal sealed record TrussModel(
        ImmutableArray<AssemblyModel> Assemblies,
        ImmutableArray<Diagnostic> Diagnostics,
        bool MessagingRuntimePresent,
        bool JobsRuntimePresent)
    {
        public static readonly TrussModel Empty = new(
            ImmutableArray<AssemblyModel>.Empty,
            ImmutableArray<Diagnostic>.Empty,
            MessagingRuntimePresent: false,
            JobsRuntimePresent: false);
    }
}
