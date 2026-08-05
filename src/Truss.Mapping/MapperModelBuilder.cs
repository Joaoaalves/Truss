using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Truss.Mapping
{
    internal static class MapperModelBuilder
    {
        private static readonly SymbolDisplayFormat FullyQualified = SymbolDisplayFormat.FullyQualifiedFormat;

        private sealed record AvailableMapping(ITypeSymbol Source, ITypeSymbol Target, string MethodName);

        public static MapperModel Build(INamedTypeSymbol mapperClass, Compilation compilation)
        {
            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            if (!IsPartial(mapperClass) || mapperClass.ContainingType is not null)
            {
                diagnostics.Add(Diagnostic.Create(
                    MappingDiagnostics.InvalidMapper,
                    Location(mapperClass),
                    $"Mapper class {mapperClass.Name} must be a non-nested partial class."));

                return new MapperModel(null, null, diagnostics.ToImmutable());
            }

            var methods = mapperClass.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(method => method.MethodKind == MethodKind.Ordinary)
                .ToList();

            var available = methods
                .Where(method => method.IsStatic && !method.ReturnsVoid && method.Parameters.Length == 1)
                .Select(method => new AvailableMapping(method.Parameters[0].Type, method.ReturnType, method.Name))
                .ToList();

            var requests = methods
                .Where(method => method.IsPartialDefinition && method.PartialImplementationPart is null)
                .ToList();

            if (requests.Count == 0)
                return new MapperModel(null, null, diagnostics.ToImmutable());

            var bodies = new List<string>();

            foreach (var request in requests)
            {
                if (!request.IsStatic || request.ReturnsVoid || request.Parameters.Length != 1)
                {
                    diagnostics.Add(Diagnostic.Create(
                        MappingDiagnostics.InvalidMapper,
                        Location(request),
                        $"Mapping method {request.Name} must be static, return the target type and take exactly one parameter."));
                    continue;
                }

                var body = BuildMethod(request, available, compilation, diagnostics);

                if (body is not null)
                    bodies.Add(body);
            }

            if (bodies.Count == 0)
                return new MapperModel(null, null, diagnostics.ToImmutable());

            var source = Emit(mapperClass, bodies);
            var hintName = $"{mapperClass.Name}.Mappings.g.cs";

            return new MapperModel(hintName, source, diagnostics.ToImmutable());
        }

        private static string? BuildMethod(
            IMethodSymbol request,
            List<AvailableMapping> available,
            Compilation compilation,
            ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var sourceParameter = request.Parameters[0];
            var sourceType = sourceParameter.Type;
            var targetType = request.ReturnType;
            var sourceProperties = ReadableProperties(sourceType);

            var constructor = SelectConstructor(targetType, sourceType, sourceParameter.Name, sourceProperties, available, compilation, out var constructorArgs);

            if (constructor is null)
            {
                ReportBestFailure(request, targetType, sourceType, sourceParameter.Name, sourceProperties, available, compilation, diagnostics);
                return null;
            }

            var covered = new HashSet<string>(
                constructor.Parameters.Select(parameter => parameter.Name),
                System.StringComparer.OrdinalIgnoreCase);

            var initializers = new List<string>();
            var failed = false;

            foreach (var property in SettableProperties(targetType))
            {
                if (covered.Contains(property.Name))
                    continue;

                var expression = Resolve(sourceParameter.Name, sourceType, sourceProperties, property.Name, property.Type, available, compilation);

                if (expression is null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        MappingDiagnostics.UnmappedMember,
                        Location(request),
                        property.Name,
                        targetType.ToDisplayString()));
                    failed = true;
                    continue;
                }

                initializers.Add($"{property.Name} = {expression}");
            }

            if (failed)
                return null;

            var builder = new StringBuilder();
            var accessibility = SyntaxFacts.GetText(request.DeclaredAccessibility);
            var returnDisplay = targetType.ToDisplayString(FullyQualified);
            var parameterDisplay = $"{sourceType.ToDisplayString(FullyQualified)} {sourceParameter.Name}";

            builder.Append("        ").Append(accessibility).Append(" static partial ")
                .Append(returnDisplay).Append(' ').Append(request.Name)
                .Append('(').Append(parameterDisplay).AppendLine(")");
            builder.AppendLine("        {");
            builder.Append("            return new ").Append(returnDisplay)
                .Append('(').Append(string.Join(", ", constructorArgs)).Append(')');

            if (initializers.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("            {");

                for (var i = 0; i < initializers.Count; i++)
                {
                    builder.Append("                ").Append(initializers[i]);
                    builder.AppendLine(i < initializers.Count - 1 ? "," : string.Empty);
                }

                builder.Append("            }");
            }

            builder.AppendLine(";");
            builder.Append("        }");

            return builder.ToString();
        }

        private static IMethodSymbol? SelectConstructor(
            ITypeSymbol targetType,
            ITypeSymbol sourceType,
            string sourceName,
            List<IPropertySymbol> sourceProperties,
            List<AvailableMapping> available,
            Compilation compilation,
            out List<string> arguments)
        {
            arguments = [];

            if (targetType is not INamedTypeSymbol named)
                return null;

            var candidates = named.InstanceConstructors
                .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public)
                .Where(constructor => !IsCopyConstructor(constructor, named))
                .OrderByDescending(constructor => constructor.Parameters.Length);

            foreach (var candidate in candidates)
            {
                var args = new List<string>();
                var success = true;

                foreach (var parameter in candidate.Parameters)
                {
                    var expression = Resolve(sourceName, sourceType, sourceProperties, parameter.Name, parameter.Type, available, compilation);

                    if (expression is null)
                    {
                        success = false;
                        break;
                    }

                    args.Add(expression);
                }

                if (success)
                {
                    arguments = args;
                    return candidate;
                }
            }

            return null;
        }

        private static void ReportBestFailure(
            IMethodSymbol request,
            ITypeSymbol targetType,
            ITypeSymbol sourceType,
            string sourceName,
            List<IPropertySymbol> sourceProperties,
            List<AvailableMapping> available,
            Compilation compilation,
            ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var named = targetType as INamedTypeSymbol;

            var best = named?.InstanceConstructors
                .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public)
                .Where(constructor => !IsCopyConstructor(constructor, named!))
                .OrderByDescending(constructor => constructor.Parameters.Length)
                .FirstOrDefault();

            if (best is null)
            {
                diagnostics.Add(Diagnostic.Create(
                    MappingDiagnostics.InvalidMapper,
                    Location(request),
                    $"{targetType.ToDisplayString()} has no accessible constructor."));
                return;
            }

            foreach (var parameter in best.Parameters)
            {
                if (Resolve(sourceName, sourceType, sourceProperties, parameter.Name, parameter.Type, available, compilation) is null)
                {
                    diagnostics.Add(Diagnostic.Create(
                        MappingDiagnostics.UnmappedMember,
                        Location(request),
                        parameter.Name,
                        targetType.ToDisplayString()));
                }
            }
        }

        private static string? Resolve(
            string sourceExpression,
            ITypeSymbol sourceType,
            List<IPropertySymbol> sourceProperties,
            string targetName,
            ITypeSymbol targetType,
            List<AvailableMapping> available,
            Compilation compilation)
        {
            var property = sourceProperties.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, targetName, System.StringComparison.OrdinalIgnoreCase));

            if (property is not null
                && Convert($"{sourceExpression}.{property.Name}", property.Type, targetType, available, compilation) is { } converted)
                return converted;

            var custom = available.FirstOrDefault(candidate =>
                string.Equals(candidate.MethodName, targetName, System.StringComparison.OrdinalIgnoreCase)
                && SymbolEqualityComparer.Default.Equals(candidate.Source, sourceType)
                && IsAssignable(candidate.Target, targetType, compilation));

            if (custom is not null)
                return $"{custom.MethodName}({sourceExpression})";

            return null;
        }

        private static string? Convert(
            string expression,
            ITypeSymbol sourceType,
            ITypeSymbol targetType,
            List<AvailableMapping> available,
            Compilation compilation)
        {
            if (IsAssignable(sourceType, targetType, compilation))
                return expression;

            if (TypedIdValueType(sourceType) is { } valueType && IsAssignable(valueType, targetType, compilation))
                return $"{expression}.Value";

            var mapping = available.FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate.Source, sourceType)
                && SymbolEqualityComparer.Default.Equals(candidate.Target, targetType));

            if (mapping is not null)
                return $"{mapping.MethodName}({expression})";

            return ConvertCollection(expression, sourceType, targetType, available, compilation);
        }

        private static string? ConvertCollection(
            string expression,
            ITypeSymbol sourceType,
            ITypeSymbol targetType,
            List<AvailableMapping> available,
            Compilation compilation)
        {
            if (sourceType.SpecialType == SpecialType.System_String || targetType.SpecialType == SpecialType.System_String)
                return null;

            var sourceElement = ElementType(sourceType);
            var (targetElement, materializer) = TargetCollection(targetType);

            if (sourceElement is null || targetElement is null || materializer is null)
                return null;

            var elementExpression = Convert("x", sourceElement, targetElement, available, compilation);

            if (elementExpression is null)
                return null;

            var projected = elementExpression == "x"
                ? expression
                : $"global::System.Linq.Enumerable.Select({expression}, x => {elementExpression})";

            return $"global::System.Linq.Enumerable.{materializer}({projected})";
        }

        private static ITypeSymbol? ElementType(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
                return array.ElementType;

            if (type is INamedTypeSymbol named && named.IsGenericType
                && named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                return named.TypeArguments[0];

            return type.AllInterfaces
                .FirstOrDefault(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                ?.TypeArguments[0];
        }

        private static (ITypeSymbol? Element, string? Materializer) TargetCollection(ITypeSymbol type)
        {
            if (type is IArrayTypeSymbol array)
                return (array.ElementType, "ToArray");

            if (type is not INamedTypeSymbol named || !named.IsGenericType)
                return (null, null);

            var definition = named.OriginalDefinition.ToDisplayString();

            return definition switch
            {
                "System.Collections.Generic.List<T>" => (named.TypeArguments[0], "ToList"),
                "System.Collections.Generic.IList<T>" => (named.TypeArguments[0], "ToList"),
                "System.Collections.Generic.ICollection<T>" => (named.TypeArguments[0], "ToList"),
                "System.Collections.Generic.IReadOnlyList<T>" => (named.TypeArguments[0], "ToList"),
                "System.Collections.Generic.IReadOnlyCollection<T>" => (named.TypeArguments[0], "ToList"),
                "System.Collections.Generic.IEnumerable<T>" => (named.TypeArguments[0], "ToList"),
                _ => (null, null)
            };
        }

        private static bool IsAssignable(ITypeSymbol sourceType, ITypeSymbol targetType, Compilation compilation)
        {
            var conversion = compilation.ClassifyCommonConversion(sourceType, targetType);
            return conversion.IsIdentity || (conversion.IsImplicit && !conversion.IsUserDefined);
        }

        private static ITypeSymbol? TypedIdValueType(ITypeSymbol type)
        {
            for (var current = type.BaseType; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType
                    && current.OriginalDefinition.ToDisplayString() == "Truss.Domain.TypedId<TValue>")
                    return current.TypeArguments[0];
            }

            return null;
        }

        private static List<IPropertySymbol> ReadableProperties(ITypeSymbol type)
        {
            var properties = new List<IPropertySymbol>();

            for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                properties.AddRange(current.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(property => !property.IsStatic
                        && property.GetMethod is { DeclaredAccessibility: Accessibility.Public }
                        && !property.IsIndexer
                        && !properties.Any(existing => existing.Name == property.Name)));
            }

            return properties;
        }

        private static IEnumerable<IPropertySymbol> SettableProperties(ITypeSymbol type)
        {
            var properties = new List<IPropertySymbol>();

            for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
            {
                properties.AddRange(current.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(property => !property.IsStatic
                        && property.SetMethod is { DeclaredAccessibility: Accessibility.Public }
                        && !property.IsIndexer
                        && !properties.Any(existing => existing.Name == property.Name)));
            }

            return properties;
        }

        private static bool IsCopyConstructor(IMethodSymbol constructor, INamedTypeSymbol type)
        {
            return constructor.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, type);
        }

        private static bool IsPartial(INamedTypeSymbol type)
        {
            return type.DeclaringSyntaxReferences
                .Select(reference => reference.GetSyntax())
                .OfType<ClassDeclarationSyntax>()
                .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        }

        private static Location Location(ISymbol symbol)
        {
            return symbol.Locations.FirstOrDefault() ?? Microsoft.CodeAnalysis.Location.None;
        }

        private static string Emit(INamedTypeSymbol mapperClass, List<string> bodies)
        {
            var builder = new StringBuilder();

            builder.AppendLine("// <auto-generated/>");
            builder.AppendLine("#nullable enable");
            builder.AppendLine();

            var hasNamespace = !mapperClass.ContainingNamespace.IsGlobalNamespace;

            if (hasNamespace)
            {
                builder.Append("namespace ").AppendLine(mapperClass.ContainingNamespace.ToDisplayString());
                builder.AppendLine("{");
            }

            builder.Append("    ").Append(SyntaxFacts.GetText(mapperClass.DeclaredAccessibility))
                .Append(" static partial class ").AppendLine(mapperClass.Name);
            builder.AppendLine("    {");

            for (var i = 0; i < bodies.Count; i++)
            {
                builder.AppendLine(bodies[i]);

                if (i < bodies.Count - 1)
                    builder.AppendLine();
            }

            builder.AppendLine("    }");

            if (hasNamespace)
                builder.AppendLine("}");

            return builder.ToString();
        }
    }
}
