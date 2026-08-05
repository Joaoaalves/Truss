using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Truss.Generators
{
    internal static class TrussModelBuilder
    {
        private static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat;

        public static TrussModel Build(Compilation compilation, CancellationToken cancellationToken)
        {
            var requestDefinition = compilation.GetTypeByMetadataName("Truss.Application.IRequest`1");
            var requestHandlerDefinition = compilation.GetTypeByMetadataName("Truss.Application.IRequestHandler`2");
            var eventHandlerDefinition = compilation.GetTypeByMetadataName("Truss.Application.IDomainEventHandler`1");
            var validatorDefinition = compilation.GetTypeByMetadataName("FluentValidation.IValidator`1");

            if (requestDefinition is null || requestHandlerDefinition is null || eventHandlerDefinition is null)
                return TrussModel.Empty;

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
                var hasInaccessibleImplementations = false;
                string? anchorType = null;

                foreach (var type in GetAllTypes(assembly.GlobalNamespace))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (type.TypeKind != TypeKind.Class || type.IsAbstract)
                        continue;

                    var isRequest = false;
                    var trussInterfaces = new List<INamedTypeSymbol>();

                    foreach (var @interface in type.AllInterfaces)
                    {
                        if (!@interface.IsGenericType)
                            continue;

                        var definition = @interface.ConstructedFrom;

                        if (SymbolEqualityComparer.Default.Equals(definition, requestDefinition))
                            isRequest = true;
                        else if (SymbolEqualityComparer.Default.Equals(definition, requestHandlerDefinition)
                            || SymbolEqualityComparer.Default.Equals(definition, eventHandlerDefinition)
                            || (validatorDefinition is not null && SymbolEqualityComparer.Default.Equals(definition, validatorDefinition)))
                            trussInterfaces.Add(@interface);
                    }

                    if (isRequest && !type.IsGenericType)
                        declaredRequests.Add(type);

                    if (trussInterfaces.Count == 0 || type.IsGenericType)
                        continue;

                    if (!isSourceAssembly && !compilation.IsSymbolAccessibleWithin(type, compilation.Assembly))
                    {
                        hasInaccessibleImplementations = true;
                        continue;
                    }

                    var implementationType = type.ToDisplayString(FullyQualified);
                    anchorType ??= implementationType;

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

                if (services.Count == 0 || anchorType is null)
                    continue;

                assemblyModels.Add(new AssemblyModel(
                    assembly.Name,
                    anchorType,
                    services.Distinct().OrderBy(s => s.ServiceType).ThenBy(s => s.ImplementationType).ToImmutableArray(),
                    requestPrimes.Values.OrderBy(p => p.RequestType).ToImmutableArray(),
                    eventPrimes.OrderBy(e => e).ToImmutableArray()));
            }

            ReportRequestDiagnostics(declaredRequests, handlersByRequest, diagnostics);

            return new TrussModel(assemblyModels.ToImmutable(), diagnostics.ToImmutable());
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
            return name is "Truss.Domain"
                or "Truss.Application"
                or "Truss.Application.Abstractions"
                or "Truss.AspNetCore"
                or "Truss.Persistence.EntityFrameworkCore";
        }

        private static bool ReferencesTrussAbstractions(IAssemblySymbol assembly)
        {
            return assembly.Modules.Any(module => module.ReferencedAssemblies.Any(
                identity => identity.Name is "Truss.Application.Abstractions" or "Truss.Application"));
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
