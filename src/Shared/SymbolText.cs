using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Tralys.Ui.CodeGen;

static class SymbolText
{
    public static readonly SymbolDisplayFormat FqnNullable =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions
                | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

    public static string Fq(this ISymbol symbol) =>
        symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    public static string FqNullable(this ITypeSymbol type) => type.ToDisplayString(FqnNullable);

    public static ITypeSymbol? NullableOf(this ITypeSymbol type) =>
        type
            is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
            } nullable
            ? nullable.TypeArguments[0]
            : null;

    // A tuple's IEquatable<> names no elements, so it never matches a named tuple; its equality is its elements'.
    public static IEnumerable<ITypeSymbol>? TupleElementTypes(this ITypeSymbol type) =>
        type is INamedTypeSymbol { IsTupleType: true } tuple
            ? tuple.TupleElements.Select(e => e.Type)
            : null;

    // By metadata name, which a type parameter's name is no part of.
    public static bool IsDefinition(this ITypeSymbol type, string metadataName)
    {
        var definition = type.OriginalDefinition;
        var name = definition.ContainingNamespace is { IsGlobalNamespace: false } ns
            ? ns.ToDisplayString() + "." + definition.MetadataName
            : definition.MetadataName;
        return definition.ContainingType is null && name == metadataName;
    }

    public static bool DerivesFrom(this INamedTypeSymbol type, string fq)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.BaseType)
        {
            if (t.Fq() == fq)
                return true;
        }
        return false;
    }

    public static ImmutableArray<string> EnumMemberNames(this INamedTypeSymbol type) =>
        [
            .. type.GetMembers()
                .OfType<IFieldSymbol>()
                .Where(f => f.HasConstantValue)
                .OrderBy(f => Convert.ToInt64(f.ConstantValue, CultureInfo.InvariantCulture))
                .Select(f => f.Name),
        ];
}
