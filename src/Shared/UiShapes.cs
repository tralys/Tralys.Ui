using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tralys.Ui.CodeGen;

static class UiShapes
{
    public const string Ui = "Tralys.Ui.Ui";
    public const string Component = "Tralys.Ui.Component`1";
    public const string Placement = "Tralys.Ui.Placement";
    public const string Children = "Tralys.Ui.ChildrenAttribute";
    public const string Stories = "Tralys.Ui.StoriesAttribute`1";
    public const string TextValue = "Tralys.Ui.TextValue";

    public static readonly ImmutableArray<string> SiteNames = ["ui", "key", "file", "line"];

    public static INamedTypeSymbol? PropsOf(INamedTypeSymbol type, INamedTypeSymbol component)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, component))
                return t.TypeArguments[0] as INamedTypeSymbol;
        }
        return null;
    }

    public static bool IsComponent(INamedTypeSymbol type, INamedTypeSymbol component) =>
        type is { TypeKind: TypeKind.Class, IsAbstract: false, IsStatic: false }
        && PropsOf(type, component) is { TypeKind: TypeKind.Struct };

    public static ImmutableArray<IPropertySymbol> Members(INamedTypeSymbol type) =>
        [
            .. type.GetMembers()
                .OfType<IPropertySymbol>()
                .Where(p =>
                    p
                        is {
                            IsStatic: false,
                            IsIndexer: false,
                            DeclaredAccessibility: Accessibility.Public,
                            SetMethod.DeclaredAccessibility: Accessibility.Public
                        }
                ),
        ];

    public static string Chain(INamedTypeSymbol type, string separator)
    {
        var names = new List<string>();
        for (var t = type; t is not null; t = t.ContainingType)
            names.Add(t.Name);
        names.Reverse();
        return string.Join(separator, names);
    }

    // Argument names are public API: "ZIndex" → "zIndex", "URLPath" → "urlPath", "ID" → "id".
    public static string Camel(string name)
    {
        if (name.Length == 0 || !char.IsUpper(name[0]))
            return name;
        var upper = 0;
        while (upper < name.Length && char.IsUpper(name[upper]))
            upper++;
        if (upper == name.Length)
            return name.ToLowerInvariant();
        var lowered = upper > 1 && char.IsLower(name[upper]) ? upper - 1 : upper;
        return name.Substring(0, lowered).ToLowerInvariant() + name.Substring(lowered);
    }

    public static bool HasTypeParameter(ITypeSymbol type) =>
        type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => HasTypeParameter(array.ElementType),
            INamedTypeSymbol named => named.TypeArguments.Any(HasTypeParameter)
                || named.ContainingType is { } outer && HasTypeParameter(outer),
            _ => false,
        };

    public static INamedTypeSymbol? NotPartial(INamedTypeSymbol type)
    {
        INamedTypeSymbol? outermost = null;
        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (
                !t.DeclaringSyntaxReferences.Any(r =>
                    r.GetSyntax() is MemberDeclarationSyntax declaration
                    && declaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword))
                )
            )
                outermost = t;
        }
        return outermost;
    }

    public const string GeneratedBy = "Tralys.Ui.Generators";

    public static IMethodSymbol? OwnStaticConstructor(INamedTypeSymbol type) =>
        type.StaticConstructors.FirstOrDefault(c =>
            !c.IsImplicitlyDeclared
            && !c.GetAttributes()
                .Any(a =>
                    a.AttributeClass?.ToDisplayString()
                        == "System.CodeDom.Compiler.GeneratedCodeAttribute"
                    && a.ConstructorArguments.FirstOrDefault().Value is GeneratedBy
                )
        );

    public static ImmutableHashSet<string> ReservedNames(INamedTypeSymbol? placement)
    {
        var names = SiteNames.ToBuilder();
        if (placement is not null)
            names.AddRange(Members(placement).Select(p => Camel(p.Name)));
        return ImmutableHashSet.CreateRange(StringComparer.Ordinal, names);
    }

    public enum Reach
    {
        None,
        Assembly,
        Public,
    }

    public static Reach ReachOf(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return ReachOf(array.ElementType);
            case ITypeParameterSymbol or IDynamicTypeSymbol:
                return Reach.Public;
            case INamedTypeSymbol named:
                var reach = Reach.Public;
                for (INamedTypeSymbol? t = named; t is not null; t = t.ContainingType)
                    reach = Min(reach, ReachOf(t.DeclaredAccessibility));
                foreach (var argument in named.TypeArguments)
                    reach = Min(reach, ReachOf(argument));
                return reach;
            default:
                return Reach.None;
        }
    }

    static Reach ReachOf(Accessibility accessibility) =>
        accessibility switch
        {
            Accessibility.Public => Reach.Public,
            Accessibility.Internal or Accessibility.ProtectedOrInternal => Reach.Assembly,
            _ => Reach.None,
        };

    static Reach Min(Reach a, Reach b) => a < b ? a : b;
}
