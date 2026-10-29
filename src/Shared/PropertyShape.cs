using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tralys.Ui.CodeGen;

readonly record struct HookShape(bool Implemented, string? Declaration);

readonly record struct Accessors(
    string Modifiers,
    string? GetterModifier,
    string? SetterKeyword,
    string SetterModifier
)
{
    public static Accessors From(IPropertySymbol property, PropertyDeclarationSyntax declaration) =>
        new(
            string.Join(" ", declaration.Modifiers.Select(m => m.Text)),
            property.GetMethod is null ? null : Modifier(property, property.GetMethod),
            property.SetMethod is null ? null
                : property.SetMethod.IsInitOnly ? "init"
                : "set",
            property.SetMethod is null ? "" : Modifier(property, property.SetMethod)
        );

    static string Modifier(IPropertySymbol property, IMethodSymbol accessor) =>
        accessor.DeclaredAccessibility == property.DeclaredAccessibility
            ? ""
            : SyntaxFacts.GetText(accessor.DeclaredAccessibility) + " ";
}

static class PropertyShape
{
    public static HookShape Hook(INamedTypeSymbol owner, string name, ITypeSymbol valueType) =>
        Find(owner, GeneratedMembers.Hook(name), valueType, RefKind.None);

    public static HookShape Coerce(INamedTypeSymbol owner, string name, ITypeSymbol valueType) =>
        Find(owner, GeneratedMembers.Coerce(name), valueType, RefKind.Ref);

    // Only a lone implementing part needs its defining declaration emitted.
    static HookShape Find(
        INamedTypeSymbol owner,
        string hook,
        ITypeSymbol valueType,
        RefKind newValue
    )
    {
        foreach (var member in owner.GetMembers(hook))
        {
            if (
                member is not IMethodSymbol { ReturnsVoid: true } method
                || method.IsStatic
                || !TakesChange(method, valueType, newValue)
            )
                continue;

            if (method.IsPartialDefinition)
                return new HookShape(method.PartialImplementationPart is not null, null);

            if (
                method
                    .DeclaringSyntaxReferences.Select(r => r.GetSyntax())
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault()
                    is not { } syntax
                || !syntax.Modifiers.Any(SyntaxKind.PartialKeyword)
            )
                return new HookShape(true, null);

            var modifiers = syntax
                .Modifiers.Where(m => !m.IsKind(SyntaxKind.AsyncKeyword))
                .Select(m => m.Text);
            var parameters = method.Parameters.Select(p =>
                $"{(p.RefKind == RefKind.Ref ? "ref " : "")}{p.Type.FqNullable()} {Literals.Identifier(p.Name)}"
            );
            return new HookShape(
                true,
                $"{string.Join(" ", modifiers)} void {method.Name}({string.Join(", ", parameters)});"
            );
        }

        var value = valueType.FqNullable();
        var by = newValue == RefKind.Ref ? "ref " : "";
        return new HookShape(
            false,
            $"partial void {hook}({value} oldValue, {by}{value} newValue);"
        );
    }

    public static bool IsEquatable(ITypeSymbol type) =>
        type is not { IsValueType: true, TypeKind: TypeKind.Struct }
        || type.NullableOf() is not null
        || type.TupleElementTypes() is { } elements && elements.All(IsEquatable)
        || type.AllInterfaces.Any(i =>
            i.OriginalDefinition.ToDisplayString() == "System.IEquatable<T>"
            && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type)
        );

    static bool TakesChange(IMethodSymbol method, ITypeSymbol valueType, RefKind newValue)
    {
        var parameters = method.Parameters;
        if (
            parameters.Length != 2
            || parameters[0].RefKind != RefKind.None
            || parameters[1].RefKind != newValue
        )
            return false;

        return SymbolEqualityComparer.Default.Equals(parameters[0].Type, valueType)
            && SymbolEqualityComparer.Default.Equals(parameters[1].Type, valueType);
    }
}
