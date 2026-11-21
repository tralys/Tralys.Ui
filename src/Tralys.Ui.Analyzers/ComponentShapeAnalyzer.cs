using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

/// <summary>Holds components and their props to the shapes the generator and the core rely on. A component is sealed,
/// keeps no instance state, has a public parameterless constructor, and is reachable from its namespace, where its call
/// is generated; one whose props are generic in its type parameters is partial, every type holding it too, with no
/// static constructor of its own, since the generator writes one that registers the props' shape (TRL3006). Its props
/// are a <c>readonly record struct</c> of <c>{ get; init; }</c> members with no initializers or positional parameters,
/// each a reference type, a primitive, an enum, a <c>Nullable&lt;T&gt;</c> or an <c>IEquatable&lt;T&gt;</c> struct,
/// and none taking the argument name of a placement or call-site argument; a member of a non-nullable reference type,
/// or of a type parameter that may be one, is <c>required</c>, since a call that omits it leaves it null
/// (TRL3001).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ComponentShapeAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor PropsShape = Descriptors.Error(
        "TRL3001",
        "Props are not a readonly record struct of { get; init; } members",
        "'{0}' {1}"
    );

    static readonly DiagnosticDescriptor ComponentShape = Descriptors.Error(
        "TRL3006",
        "A component is not a sealed, stateless class",
        "The component '{0}' {1}"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [PropsShape, ComponentShape];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (UiCore.From(start.Compilation) is not { } core)
                return;
            var checkedProps = new ConcurrentDictionary<INamedTypeSymbol, byte>(
                SymbolEqualityComparer.Default
            );
            start.RegisterSymbolAction(
                c => Analyze(c, core, (INamedTypeSymbol)c.Symbol, checkedProps),
                SymbolKind.NamedType
            );
        });
    }

    static void Analyze(
        SymbolAnalysisContext context,
        UiCore core,
        INamedTypeSymbol type,
        ConcurrentDictionary<INamedTypeSymbol, byte> checkedProps
    )
    {
        if (
            !UiShapes.IsComponent(type, core.Component)
            || UiShapes.PropsOf(type, core.Component) is not { } props
        )
            return;

        void Component(Location? at, string problem) =>
            context.ReportDiagnostic(Diagnostic.Create(ComponentShape, at, type.Name, problem));

        var here = type.Locations.FirstOrDefault();
        if (!type.IsSealed)
            Component(here, "is not sealed");
        if (
            !type.InstanceConstructors.Any(c =>
                c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public
            )
        )
            Component(
                here,
                "has no public parameterless constructor, which the core makes its one instance with"
            );
        if (UiShapes.ReachOf(type) == UiShapes.Reach.None)
            Component(
                here,
                "is private or protected, but its call is generated at namespace level"
            );
        if (UiShapes.HasTypeParameter(props))
        {
            if (UiShapes.NotPartial(type) is { } whole)
                Component(
                    here,
                    SymbolEqualityComparer.Default.Equals(whole, type)
                        ? "has props generic in its type parameters but isn't partial; the generator registers their shape from a static constructor it writes into the component"
                        : $"has props generic in its type parameters but '{whole.Name}', which holds it, isn't partial; the generator registers their shape from a static constructor it writes into the component"
                );
            else if (UiShapes.OwnStaticConstructor(type) is { } constructor)
                Component(
                    constructor.Locations.FirstOrDefault() ?? here,
                    "has props generic in its type parameters and a static constructor of its own, where the generator registers their shape"
                );
        }

        for (
            var t = type;
            t is not null
                && !SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, core.Component);
            t = t.BaseType
        )
        {
            foreach (var field in t.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.IsStatic || field.IsConst)
                    continue;
                if (
                    field is
                    { IsImplicitlyDeclared: true, AssociatedSymbol: IPropertySymbol property }
                )
                    Component(
                        property.Locations.FirstOrDefault() ?? here,
                        $"has the auto-property '{property.Name}'; the one instance is shared, so its state lives in hooks"
                    );
                else if (!field.IsImplicitlyDeclared)
                    Component(
                        field.Locations.FirstOrDefault() ?? here,
                        $"has the instance field '{field.Name}'; the one instance is shared, so its state lives in hooks"
                    );
            }
        }

        if (
            props.Locations.Any(l => l.IsInSource)
            && checkedProps.TryAdd(props.OriginalDefinition, 0)
        )
            Props(context, core, props.OriginalDefinition);
    }

    static void Props(SymbolAnalysisContext context, UiCore core, INamedTypeSymbol props)
    {
        void Report(Location? at, string problem) =>
            context.ReportDiagnostic(
                Diagnostic.Create(
                    PropsShape,
                    at ?? props.Locations.FirstOrDefault(),
                    props.Name,
                    problem
                )
            );

        var declarations = props
            .DeclaringSyntaxReferences.Select(r => r.GetSyntax(context.CancellationToken))
            .ToList();
        if (props is not { IsRecord: true, TypeKind: TypeKind.Struct, IsReadOnly: true })
            Report(null, "must be a readonly record struct");
        if (declarations.OfType<RecordDeclarationSyntax>().Any(d => d.ParameterList is not null))
            Report(null, "must not take positional parameters; declare { get; init; } members");

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var member in props.GetMembers())
        {
            var at = member.Locations.FirstOrDefault();
            switch (member)
            {
                case IFieldSymbol { IsStatic: false, IsConst: false, IsImplicitlyDeclared: false }:
                    Report(
                        at,
                        $"has the field '{member.Name}'; props hold only {{ get; init; }} members"
                    );
                    break;
                case IPropertySymbol
                {
                    IsStatic: false,
                    IsIndexer: false,
                    IsImplicitlyDeclared: false
                } property:
                    if (
                        property.GetMethod is null
                        || property.SetMethod is not { IsInitOnly: true }
                    )
                        Report(at, $"has '{property.Name}', which must be {{ get; init; }}");
                    if (
                        property
                            .DeclaringSyntaxReferences.Select(r =>
                                r.GetSyntax(context.CancellationToken)
                            )
                            .OfType<PropertyDeclarationSyntax>()
                            .Any(p => p.Initializer is not null)
                    )
                        Report(
                            at,
                            $"has '{property.Name}' with an initializer; zero or null means the component's default"
                        );
                    if (!Allowed(property.Type, core))
                        Report(
                            at,
                            $"has '{property.Name}' of type '{property.Type.ToDisplayString()}', a struct that isn't IEquatable<{property.Type.ToDisplayString()}>, so comparing props would box it"
                        );
                    if (Omittable(property))
                        Report(
                            at,
                            $"has '{property.Name}' of non-nullable type '{property.Type.ToDisplayString()}', which a call may omit; mark it required, or declare it '{property.Type.ToDisplayString()}?'"
                        );
                    var argument = UiShapes.Camel(property.Name);
                    if (core.Reserved.Contains(argument))
                        Report(
                            at,
                            $"has '{property.Name}', whose argument '{argument}' a placement or call-site argument already takes"
                        );
                    else if (names.TryGetValue(argument, out var other))
                        Report(
                            at,
                            $"has '{property.Name}' and '{other}', which take the same argument '{argument}'"
                        );
                    else
                        names.Add(argument, property.Name);
                    break;
            }
        }
    }

    static bool Omittable(IPropertySymbol property) =>
        !property.IsRequired
        && property.Type
            is { NullableAnnotation: NullableAnnotation.NotAnnotated, IsValueType: false }
        && (property.Type.IsReferenceType || property.Type is ITypeParameterSymbol);

    static bool Allowed(ITypeSymbol type, UiCore core) =>
        type.IsReferenceType
        || type is ITypeParameterSymbol
        || type.TypeKind == TypeKind.Enum
        || type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_Double
        || type.NullableOf() is not null
        || (
            type.TupleElementTypes() is { } elements
                ? elements.All(e => Allowed(e, core))
                : core.IsEquatable(type)
        );
}
