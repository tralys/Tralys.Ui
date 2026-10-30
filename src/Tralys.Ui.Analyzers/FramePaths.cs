using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Tralys.Ui.Analyzers;

static class FramePaths
{
    public const string Attribute = "Tralys.Ui.FramePathAttribute";

    const string ColdAttribute = "Tralys.Ui.ColdPathAttribute";

    public static bool IsCold(ISymbol symbol) =>
        HasAttribute(symbol, ColdAttribute)
        || symbol is IMethodSymbol { AssociatedSymbol: { } associated }
            && HasAttribute(associated, ColdAttribute);

    public static bool OnFramePath(ISymbol symbol)
    {
        for (var s = symbol; s is not null and not INamespaceSymbol; s = s.ContainingSymbol)
        {
            if (
                MarkedOrInherited(s)
                || s is IMethodSymbol { AssociatedSymbol: { } associated }
                    && MarkedOrInherited(associated)
            )
                return true;
        }
        return false;
    }

    public static bool IsCarrier(IAnonymousFunctionOperation lambda)
    {
        if (lambda.Parent is not IDelegateCreationOperation creation)
            return false;
        if (creation.Type is INamedTypeSymbol type && Marked(type.OriginalDefinition))
            return true;
        var use = creation.Parent;
        while (use is IConversionOperation)
            use = use.Parent;
        return use is IArgumentOperation { Parameter: { } parameter } && Marked(parameter);
    }

    public static bool Marked(ISymbol symbol) => HasAttribute(symbol, Attribute);

    static bool HasAttribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == name);

    static bool MarkedOrInherited(ISymbol symbol)
    {
        for (var s = symbol; s is not null; s = Overridden(s))
        {
            if (Marked(s) || Implemented(s).Any(Marked))
                return true;
        }
        return false;
    }

    static ISymbol? Overridden(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol method => method.OverriddenMethod,
            IPropertySymbol property => property.OverriddenProperty,
            _ => null,
        };

    static IEnumerable<ISymbol> Implemented(ISymbol symbol)
    {
        ImmutableArray<ISymbol> explicitly;
        switch (symbol)
        {
            case IMethodSymbol method:
                explicitly = method.ExplicitInterfaceImplementations.CastArray<ISymbol>();
                break;
            case IPropertySymbol property:
                explicitly = property.ExplicitInterfaceImplementations.CastArray<ISymbol>();
                break;
            default:
                yield break;
        }
        foreach (var member in explicitly)
            yield return member;

        var type = symbol.ContainingType;
        foreach (var contract in type.AllInterfaces)
        {
            foreach (var member in contract.GetMembers(symbol.Name))
            {
                if (
                    SymbolEqualityComparer.Default.Equals(
                        type.FindImplementationForInterfaceMember(member),
                        symbol
                    )
                )
                    yield return member;
            }
        }
    }
}
