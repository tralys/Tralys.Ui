using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

sealed class UiCore
{
    const string CallerFile = "System.Runtime.CompilerServices.CallerFilePathAttribute";
    const string CallerLine = "System.Runtime.CompilerServices.CallerLineNumberAttribute";

    UiCore(Compilation compilation, INamedTypeSymbol ui, INamedTypeSymbol component)
    {
        Ui = ui;
        Component = component;
        Scope = compilation.GetTypeByMetadataName("Tralys.Ui.Scope");
        UiKey = compilation.GetTypeByMetadataName("Tralys.Ui.UiKey");
        Stories = compilation.GetTypeByMetadataName(UiShapes.Stories);
        Slot = compilation.GetTypeByMetadataName("Tralys.Ui.Slot`1");
        Resettable = compilation.GetTypeByMetadataName("Tralys.Ui.IResettable");
        Equatable = compilation.GetTypeByMetadataName("System.IEquatable`1");
        var placement = compilation.GetTypeByMetadataName(UiShapes.Placement);
        Placement = placement is null
            ? ImmutableDictionary<string, ITypeSymbol>.Empty
            : UiShapes
                .Members(placement)
                .ToImmutableDictionary(
                    p => UiShapes.Camel(p.Name),
                    p => p.Type,
                    StringComparer.Ordinal
                );
        Reserved = UiShapes.ReservedNames(placement);
    }

    public INamedTypeSymbol Ui { get; }

    public INamedTypeSymbol Component { get; }

    public INamedTypeSymbol? Scope { get; }

    public INamedTypeSymbol? UiKey { get; }

    public INamedTypeSymbol? Stories { get; }

    public INamedTypeSymbol? Slot { get; }

    public INamedTypeSymbol? Resettable { get; }

    public INamedTypeSymbol? Equatable { get; }

    public ImmutableDictionary<string, ITypeSymbol> Placement { get; }

    // By type too: a call's own argument may share a placement member's name.
    public bool IsPlacement(IParameterSymbol parameter) =>
        Placement.TryGetValue(parameter.Name, out var type)
        && SymbolEqualityComparer.Default.Equals(parameter.Type, type);

    public ImmutableHashSet<string> Reserved { get; }

    public static UiCore? From(Compilation compilation) =>
        compilation.GetTypeByMetadataName(UiShapes.Ui) is { } ui
        && compilation.GetTypeByMetadataName(UiShapes.Component) is { } component
            ? new UiCore(compilation, ui, component)
            : null;

    public bool IsUi(ITypeSymbol? type) => SymbolEqualityComparer.Default.Equals(type, Ui);

    public bool TakesUi(IMethodSymbol method) => method.Parameters.Any(p => IsUi(p.Type));

    public bool OnUi(IInvocationOperation invocation) =>
        invocation.TargetMethod.IsExtensionMethod
            ? invocation.TargetMethod.Parameters.Length > 0
                && IsUi(invocation.TargetMethod.Parameters[0].Type)
            : invocation.Instance is not null && IsUi(invocation.Instance.Type);

    public bool ReturnsScope(IMethodSymbol method) =>
        Scope is not null && SymbolEqualityComparer.Default.Equals(method.ReturnType, Scope);

    public bool CarriesIdentity(IInvocationOperation invocation) =>
        OnUi(invocation) && LineOf(invocation.TargetMethod) is not null;

    public bool IsHook(IMethodSymbol method) =>
        !method.IsExtensionMethod && IsUi(method.ContainingType) && !ReturnsScope(method);

    public bool IsFragment(IMethodSymbol method) =>
        !method.IsExtensionMethod && IsUi(method.ContainingType) && method.Name == "Fragment";

    // Fragment and ActionScope nodes take no part in layout, so the first node inside one is a component's root.
    public bool IsTransparent(IMethodSymbol method) =>
        !method.IsExtensionMethod
        && IsUi(method.ContainingType)
        && method.Name is "Fragment" or "ActionScope";

    public bool HasKey(IInvocationOperation invocation) =>
        invocation.Arguments.Any(a =>
            a.ArgumentKind != ArgumentKind.DefaultValue
            && SymbolEqualityComparer.Default.Equals(a.Parameter?.Type, UiKey)
        );

    public bool IsGeneratedShape(IMethodSymbol method)
    {
        var parameters = method.Parameters;
        return method.IsExtensionMethod
            && parameters.Length >= 4
            && IsUi(parameters[0].Type)
            && SymbolEqualityComparer.Default.Equals(parameters[parameters.Length - 3].Type, UiKey)
            && Has(parameters[parameters.Length - 2], CallerFile)
            && Has(parameters[parameters.Length - 1], CallerLine);
    }

    public bool IsRender(IMethodSymbol method)
    {
        for (var m = method.OverriddenMethod; m is not null; m = m.OverriddenMethod)
        {
            if (
                m.Name == "Render"
                && SymbolEqualityComparer.Default.Equals(
                    m.ContainingType.OriginalDefinition,
                    Component
                )
            )
                return true;
        }
        return false;
    }

    public bool IsStory(IMethodSymbol method) =>
        method
            is {
                MethodKind: MethodKind.Ordinary,
                IsStatic: true,
                ReturnsVoid: true,
                DeclaredAccessibility: Accessibility.Public,
                Parameters.Length: 1,
            }
        && IsUi(method.Parameters[0].Type)
        && method
            .ContainingType.GetAttributes()
            .Any(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass?.OriginalDefinition, Stories)
            );

    public bool IsEquatable(ITypeSymbol type) =>
        type.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, Equatable)
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type)
        );

    public static IParameterSymbol? FileOf(IMethodSymbol method) =>
        method.Parameters.FirstOrDefault(p =>
            p.Type.SpecialType == SpecialType.System_String && Has(p, CallerFile)
        );

    public static IParameterSymbol? LineOf(IMethodSymbol method) =>
        method.Parameters.FirstOrDefault(p =>
            p.Type.SpecialType == SpecialType.System_Int32 && Has(p, CallerLine)
        );

    static bool Has(IParameterSymbol parameter, string attribute) =>
        parameter.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == attribute);

    public List<IInvocationOperation> ScopesAround(IOperation operation, out bool inLambda)
    {
        var scopes = new List<IInvocationOperation>();
        inLambda = false;
        var child = operation;
        for (
            var parent = operation.Parent;
            parent is not null;
            child = parent, parent = parent.Parent
        )
        {
            switch (parent)
            {
                case IAnonymousFunctionOperation or ILocalFunctionOperation:
                    inLambda = true;
                    return scopes;
                case IUsingOperation { Body: var body } use
                    when body == child && ScopeIn(use.Resources) is { } resource:
                    scopes.Add(resource);
                    break;
                case IBlockOperation block:
                    for (var i = block.Operations.IndexOf(child) - 1; i >= 0; i--)
                    {
                        if (
                            block.Operations[i] is IUsingDeclarationOperation declaration
                            && ScopeIn(declaration.DeclarationGroup) is { } declared
                        )
                            scopes.Add(declared);
                    }
                    break;
            }
        }
        return scopes;
    }

    // For a hook only a fragment counts as a scope, since node scopes don't scope hooks.
    public ILoopOperation? LoopAround(IOperation operation, bool hook)
    {
        var child = operation;
        for (
            var parent = operation.Parent;
            parent is not null;
            child = parent, parent = parent.Parent
        )
        {
            switch (parent)
            {
                case IAnonymousFunctionOperation or ILocalFunctionOperation:
                    return null;
                case ILoopOperation loop when loop.Body == child:
                    return loop;
                case IUsingOperation { Body: var body } use
                    when body == child && Bounds(ScopeIn(use.Resources), hook):
                    return null;
                case IBlockOperation block:
                    for (var i = block.Operations.IndexOf(child) - 1; i >= 0; i--)
                    {
                        if (
                            block.Operations[i] is IUsingDeclarationOperation declaration
                            && Bounds(ScopeIn(declaration.DeclarationGroup), hook)
                        )
                            return null;
                    }
                    break;
            }
        }
        return null;
    }

    bool Bounds(IInvocationOperation? scope, bool hook) =>
        scope is not null && (!hook || IsFragment(scope.TargetMethod));

    IInvocationOperation? ScopeIn(IOperation resources) =>
        resources
            .DescendantsAndSelf()
            .OfType<IInvocationOperation>()
            .FirstOrDefault(i => ReturnsScope(i.TargetMethod) && OnUi(i));
}
