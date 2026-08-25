using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Truss.Generators
{
    internal static class TrussModelBuilder
    {
        // The nullable annotation is part of the type when it appears in a
        // constraint: a handler of IQuery<FooDto?> only satisfies IRequest<FooDto?>,
        // so dropping the ? makes the generated registration warn (CS8631).
        private static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat
            .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

        public static TrussModel Build(Compilation compilation, CancellationToken cancellationToken)
        {
            var requestDefinition = compilation.GetTypeByMetadataName("Truss.Application.IRequest`1");
            var requestHandlerDefinition = compilation.GetTypeByMetadataName("Truss.Application.IRequestHandler`2");
            var eventHandlerDefinition = compilation.GetTypeByMetadataName("Truss.Application.IDomainEventHandler`1");
            var validatorDefinition = compilation.GetTypeByMetadataName("FluentValidation.IValidator`1");

            if (requestDefinition is null || requestHandlerDefinition is null || eventHandlerDefinition is null)
                return TrussModel.Empty;

            // The messaging and job registrations are only emitted when the
            // runtime packages that receive them are part of this compilation;
            // otherwise those assemblies keep their reflection fallback.
            var integrationHandlerDefinition = compilation.GetTypeByMetadataName("Truss.Messaging.IIntegrationEventHandler`1");
            var integrationEventInterface = compilation.GetTypeByMetadataName("Truss.Messaging.IIntegrationEvent");
            var jobDefinition = compilation.GetTypeByMetadataName("Truss.Jobs.IJob`1");
            var messagingRuntimePresent = compilation.GetTypeByMetadataName("Truss.Messaging.Dispatch.TrussMessagingGeneratedRegistry") is not null;
            var jobsRuntimePresent = compilation.GetTypeByMetadataName("Truss.Jobs.Runtime.TrussJobsGeneratedRegistry") is not null;

            var assemblies = CollectAssemblies(compilation);

            var assemblyModels = ImmutableArray.CreateBuilder<AssemblyModel>();
            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
            var declaredRequests = new List<INamedTypeSymbol>();
            var handlersByRequest = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);

            foreach (var assembly in assemblies)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var isSourceAssembly = SymbolEqualityComparer.Default.Equals(assembly, compilation.Assembly);
                var services = new List<ServiceRegistration>();
                var requestPrimes = new Dictionary<string, RequestPrime>();
                var eventPrimes = new HashSet<string>();
                var messagingHandlers = new List<ServiceRegistration>();
                var integrationEvents = new HashSet<string>();
                var jobs = new List<JobRegistration>();
                var hasInaccessibleImplementations = false;
                string? anchorType = null;

                foreach (var type in GetAllTypes(assembly.GlobalNamespace))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (type.TypeKind != TypeKind.Class || type.IsAbstract)
                        continue;

                    var isRequest = false;
                    var isIntegrationEvent = false;
                    var trussInterfaces = new List<INamedTypeSymbol>();
                    var messagingInterfaces = new List<INamedTypeSymbol>();
                    var jobInterfaces = new List<INamedTypeSymbol>();

                    foreach (var @interface in type.AllInterfaces)
                    {
                        if (!@interface.IsGenericType)
                        {
                            if (integrationEventInterface is not null && SymbolEqualityComparer.Default.Equals(@interface, integrationEventInterface))
                                isIntegrationEvent = true;

                            continue;
                        }

                        var definition = @interface.ConstructedFrom;

                        if (SymbolEqualityComparer.Default.Equals(definition, requestDefinition))
                            isRequest = true;
                        else if (SymbolEqualityComparer.Default.Equals(definition, requestHandlerDefinition)
                            || SymbolEqualityComparer.Default.Equals(definition, eventHandlerDefinition)
                            || (validatorDefinition is not null && SymbolEqualityComparer.Default.Equals(definition, validatorDefinition)))
                            trussInterfaces.Add(@interface);
                        else if (messagingRuntimePresent && integrationHandlerDefinition is not null
                            && SymbolEqualityComparer.Default.Equals(definition, integrationHandlerDefinition))
                            messagingInterfaces.Add(@interface);
                        else if (jobsRuntimePresent && jobDefinition is not null
                            && SymbolEqualityComparer.Default.Equals(definition, jobDefinition))
                            jobInterfaces.Add(@interface);
                    }

                    if (isRequest && !type.IsGenericType)
                        declaredRequests.Add(type);

                    if (type.IsGenericType)
                        continue;

                    if (trussInterfaces.Count == 0 && messagingInterfaces.Count == 0 && jobInterfaces.Count == 0
                        && !(messagingRuntimePresent && isIntegrationEvent))
                    {
                        continue;
                    }

                    if (!isSourceAssembly && !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
                    {
                        hasInaccessibleImplementations = true;
                        continue;
                    }

                    var implementationType = type.ToDisplayString(FullyQualified);
                    anchorType ??= implementationType;

                    if (messagingRuntimePresent && isIntegrationEvent)
                        integrationEvents.Add(implementationType);

                    foreach (var @interface in messagingInterfaces)
                        messagingHandlers.Add(new ServiceRegistration(@interface.ToDisplayString(FullyQualified), implementationType));

                    foreach (var @interface in jobInterfaces)
                        jobs.Add(new JobRegistration(implementationType, @interface.TypeArguments[0].ToDisplayString(FullyQualified)));

                    foreach (var @interface in trussInterfaces)
                    {
                        var definition = @interface.ConstructedFrom;
                        services.Add(new ServiceRegistration(@interface.ToDisplayString(FullyQualified), implementationType));

                        if (SymbolEqualityComparer.Default.Equals(definition, requestHandlerDefinition))
                        {
                            var requestType = @interface.TypeArguments[0].ToDisplayString(FullyQualified);
                            var responseType = @interface.TypeArguments[1].ToDisplayString(FullyQualified);
                            requestPrimes[requestType] = new RequestPrime(requestType, responseType);

                            if (@interface.TypeArguments[0] is INamedTypeSymbol requestSymbol)
                                TrackHandler(handlersByRequest, requestSymbol, type);
                        }
                        else if (SymbolEqualityComparer.Default.Equals(definition, eventHandlerDefinition))
                        {
                            eventPrimes.Add(@interface.TypeArguments[0].ToDisplayString(FullyQualified));
                        }
                    }
                }

                if (hasInaccessibleImplementations)
                {
                    diagnostics.Add(Diagnostic.Create(
                        TrussDiagnostics.InaccessibleImplementations, Location.None, assembly.Name));
                    continue;
                }

                if (anchorType is null || (services.Count == 0 && messagingHandlers.Count == 0 && integrationEvents.Count == 0 && jobs.Count == 0))
                    continue;

                assemblyModels.Add(new AssemblyModel(
                    assembly.Name,
                    anchorType,
                    services.Distinct().OrderBy(s => s.ServiceType).ThenBy(s => s.ImplementationType).ToImmutableArray(),
                    requestPrimes.Values.OrderBy(p => p.RequestType).ToImmutableArray(),
                    eventPrimes.OrderBy(e => e).ToImmutableArray(),
                    messagingHandlers.Distinct().OrderBy(s => s.ServiceType).ThenBy(s => s.ImplementationType).ToImmutableArray(),
                    integrationEvents.OrderBy(e => e).ToImmutableArray(),
                    jobs.Distinct().OrderBy(j => j.JobType).ToImmutableArray()));
            }

            ReportRequestDiagnostics(declaredRequests, handlersByRequest, diagnostics);

            return new TrussModel(assemblyModels.ToImmutable(), diagnostics.ToImmutable(), messagingRuntimePresent, jobsRuntimePresent);
        }

        private static List<IAssemblySymbol> CollectAssemblies(Compilation compilation)
        {
            var assemblies = new List<IAssemblySymbol> { compilation.Assembly };

            foreach (var reference in compilation.References)
            {
                if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly
                    && !IsTrussPackage(assembly.Name)
                    && ReferencesTrussAbstractions(assembly))
                {
                    assemblies.Add(assembly);
                }
            }

            return assemblies;
        }

        private static bool IsTrussPackage(string name)
        {
            // The framework's own assemblies register themselves at runtime;
            // scanning them would only trip over their internal handlers, such
            // as the job runtime's own integration event handler.
            return name == "Truss" || name.StartsWith("Truss.", System.StringComparison.Ordinal);
        }

        private static bool ReferencesTrussAbstractions(IAssemblySymbol assembly)
        {
            return assembly.Modules.Any(module => module.ReferencedAssemblies.Any(
                identity => identity.Name == "Truss.Application"));
        }

        private static void TrackHandler(
            Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> handlersByRequest,
            INamedTypeSymbol request,
            INamedTypeSymbol handler)
        {
            if (!handlersByRequest.TryGetValue(request, out var handlers))
            {
                handlers = new List<INamedTypeSymbol>();
                handlersByRequest[request] = handlers;
            }

            if (!handlers.Contains(handler, SymbolEqualityComparer.Default))
                handlers.Add(handler);
        }

        private static void ReportRequestDiagnostics(
            List<INamedTypeSymbol> declaredRequests,
            Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>> handlersByRequest,
            ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            foreach (var request in declaredRequests)
            {
                handlersByRequest.TryGetValue(request, out var handlers);
                var count = handlers?.Count ?? 0;

                if (count == 0)
                {
                    // A contracts assembly declares queries whose handler lives
                    // in another service; the consuming side wires them at
                    // runtime with AddRemoteContext.
                    if (request.ContainingAssembly.Name.EndsWith(".Contracts", System.StringComparison.Ordinal))
                        continue;

                    diagnostics.Add(Diagnostic.Create(
                        TrussDiagnostics.MissingHandler,
                        LocationOf(request),
                        request.ToDisplayString()));
                }
                else if (count > 1)
                {
                    diagnostics.Add(Diagnostic.Create(
                        TrussDiagnostics.DuplicateHandler,
                        LocationOf(request),
                        request.ToDisplayString(),
                        string.Join(", ", handlers!.Select(h => h.ToDisplayString()))));
                }
            }
        }

        private static Location LocationOf(INamedTypeSymbol type)
        {
            return type.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;
        }

        private static IEnumerable<INamedTypeSymbol> GetAllTypes(INamespaceSymbol @namespace)
        {
            foreach (var member in @namespace.GetMembers())
            {
                if (member is INamespaceSymbol nested)
                {
                    foreach (var type in GetAllTypes(nested))
                        yield return type;
                }
                else if (member is INamedTypeSymbol type)
                {
                    foreach (var t in GetTypeAndNested(type))
                        yield return t;
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetTypeAndNested(INamedTypeSymbol type)
        {
            yield return type;

            foreach (var nested in type.GetTypeMembers())
            {
                foreach (var t in GetTypeAndNested(nested))
                    yield return t;
            }
        }
    }
}
