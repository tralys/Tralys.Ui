using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Generators.Icons;

/// <summary>The icon compiler: reads the app's icon set (its <c>IconCodepoints.json</c> or <c>*.icons.json</c>) and
/// emits a static <c>Icons</c> class with one typed <c>IconGlyph</c> per icon, every icon by name, and the
/// <c>IconSet</c> that draws Tralys.Ui's icon roles. A role the set does not name is a build error.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class IconGenerator : IIncrementalGenerator
{
    const string RoleNamespace = "Tralys.Ui";

    static readonly DiagnosticDescriptor IconError = Descriptors.Error(
        "TRL4101",
        "Icon set does not compile",
        "Icon set: {0}"
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context
            .AdditionalTextsProvider.Where(static f => IconTable.IsIconFile(f.Path))
            .Select(static (f, ct) => (Path: f.Path, Text: f.GetText(ct)?.ToString() ?? ""))
            .Collect();

        var fonts = context.AnalyzerConfigOptionsProvider.Select(
            static (o, _) => IconTable.Fonts(o)
        );
        var roles = context.CompilationProvider.Select(
            static (compilation, _) =>
                compilation.GetTypeByMetadataName(RoleNamespace + ".IconRole")
                    is { TypeKind: TypeKind.Enum } role
                    ? new EquatableArray<string>(role.EnumMemberNames())
                    : default
        );

        context.RegisterSourceOutput(
            files.Combine(fonts).Combine(Output.RootNamespace(context)).Combine(roles),
            static (spc, input) =>
                Emit(
                    spc,
                    input.Left.Left.Left,
                    input.Left.Left.Right,
                    input.Left.Right,
                    input.Right
                )
        );
    }

    static void Emit(
        SourceProductionContext context,
        System.Collections.Immutable.ImmutableArray<(string Path, string Text)> files,
        string fonts,
        string ns,
        EquatableArray<string> roles
    )
    {
        const string g = "global::" + RoleNamespace;
        if (files.Length == 0)
            return;
        if (files.Length > 1)
        {
            Fail(
                context,
                files[1].Path,
                $"{files.Length} icon sets are given; a project compiles one"
            );
            return;
        }

        var (path, text) = files[0];
        var icons = IconTable.Read(text, fonts, out var error);
        if (error is not null)
        {
            Fail(context, path, error);
            return;
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var icon in icons.Items)
        {
            if (!SyntaxFacts.IsValidIdentifier(icon.Name) || icon.Name is "All" or "Set" or "Icons")
            {
                Fail(context, path, $"'{icon.Name}' cannot be a member of Icons");
                return;
            }
            names.Add(icon.Name);
        }

        foreach (var role in roles.Items)
        {
            if (!names.Contains(role))
            {
                Fail(
                    context,
                    path,
                    $"Tralys.Ui draws the {role} role, which the set names no icon for"
                );
                return;
            }
        }

        var w = new CodeWriter();
        Output.Header(w);
        Output.Namespace(w, ns);

        w.Line("/// <summary>The glyphs of the app's icon set.</summary>");
        using (w.Block("public static partial class Icons"))
        {
            foreach (var icon in icons.Items)
                w.Line(
                    $"public static {g}.IconGlyph {Literals.Identifier(icon.Name)} => {IconTable.GlyphExpression(icon, RoleNamespace)};"
                );
            w.Line();
            w.Line("/// <summary>Every icon of the set by name, in the set's order.</summary>");
            w.Line(
                "public static global::System.Collections.Generic.IReadOnlyList<global::System.Collections.Generic.KeyValuePair<string, "
                    + g
                    + ".IconGlyph>> All { get; } ="
            );
            using (w.Indented())
            {
                w.Line("[");
                foreach (var icon in icons.Items)
                    w.Line(
                        $"    new({Literals.Quote(icon.Name)}, {Literals.Identifier(icon.Name)}),"
                    );
                w.Line("];");
            }
            w.Line();
            w.Line(
                "/// <summary>The glyph the set draws for each of Tralys.Ui's icon roles.</summary>"
            );
            w.Line(
                $"public static {g}.IconSet Set {{ get; }} = new([{string.Join(", ", roles.Items)}]);"
            );
        }
        Output.Add(context, "Tralys.Ui.Icons.g.cs", w.ToString());
    }

    static void Fail(SourceProductionContext context, string path, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(IconError, Output.AtFile(path), message));
}
