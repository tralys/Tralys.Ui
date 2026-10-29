using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Tralys.Ui.CodeGen;

readonly record struct Owner(
    string Key,
    string Fqn,
    string Declarations,
    string? Namespace,
    string? NotPartial,
    LocationInfo? Location
)
{
    public bool IsPartial => NotPartial is null;

    public static Owner From(INamedTypeSymbol type)
    {
        var notPartial = NotPartialIn(type);
        return new Owner(
            Keyed(type),
            type.Fq(),
            DeclarationChain(type),
            type.ContainingNamespace.IsGlobalNamespace
                ? null
                : type.ContainingNamespace.ToDisplayString(),
            notPartial?.ToDisplayString(),
            LocationInfo.From((notPartial ?? type).Locations.FirstOrDefault())
        );
    }

    public List<CodeWriter.Scope> Open(CodeWriter w)
    {
        Output.Namespace(w, Namespace);

        var scopes = new List<CodeWriter.Scope>();
        foreach (var level in Declarations.Split(';'))
            scopes.Add(w.Block($"partial {level}"));
        return scopes;
    }

    public static void Close(List<CodeWriter.Scope> scopes)
    {
        for (var i = scopes.Count - 1; i >= 0; i--)
            scopes[i].Dispose();
    }

    // The separators are characters no identifier holds, so no two types share a key.
    static string Keyed(INamedTypeSymbol type)
    {
        var levels = new Stack<string>();
        for (var current = type; current is not null; current = current.ContainingType)
            levels.Push(current.Arity == 0 ? current.Name : current.Name + "`" + current.Arity);

        var key = string.Join("+", levels);
        for (
            var ns = type.ContainingNamespace;
            ns is { IsGlobalNamespace: false };
            ns = ns.ContainingNamespace
        )
            key = ns.Name + "." + key;

        return key;
    }

    static string DeclarationChain(INamedTypeSymbol type)
    {
        var parts = new Stack<string>();
        for (var current = type; current is not null; current = current.ContainingType)
        {
            var kind = current.TypeKind switch
            {
                TypeKind.Struct => current.IsRecord ? "record struct" : "struct",
                TypeKind.Interface => "interface",
                _ => current.IsRecord ? "record" : "class",
            };
            var parameters =
                current.Arity == 0
                    ? ""
                    : "<"
                        + string.Join(
                            ", ",
                            current.TypeParameters.Select(p => Literals.Identifier(p.Name))
                        )
                        + ">";
            parts.Push($"{kind} {Literals.Identifier(current.Name)}{parameters}");
        }

        return string.Join(";", parts);
    }

    static INamedTypeSymbol? NotPartialIn(INamedTypeSymbol type)
    {
        INamedTypeSymbol? offender = null;
        for (var current = type; current is not null; current = current.ContainingType)
        {
            if (!DeclaredPartial(current))
                offender = current;
        }

        return offender;
    }

    static bool DeclaredPartial(ISymbol symbol) =>
        symbol
            .DeclaringSyntaxReferences.Select(r => r.GetSyntax())
            .OfType<MemberDeclarationSyntax>()
            .Any(d => d.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));
}
