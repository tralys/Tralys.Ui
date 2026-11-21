using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Generators.Theming;

/// <summary>The token compiler: reads the app's W3C design tokens, the <c>*.tokens.json</c> files its one
/// <c>*.resolver.json</c> names by their paths from its folder, <c>../</c> reaching the files outside it, and emits a
/// static <c>Themes</c> class holding one <c>Tralys.Ui.Theming.Theme</c> per theme context. The app's tokens lay over
/// Tralys.Ui's own, so a slot of Tralys.Ui's vocabulary they leave out keeps Tralys.Ui's value: from the theme of the same
/// context, or Tralys.Ui's default theme for a context Tralys.Ui has not. A slot neither fills is a warning.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ThemeTokenGenerator : IIncrementalGenerator
{
    const string T = "global::Tralys.Ui.Theming";

    static readonly string[] VocabularyNames =
    [
        "ColorScheme",
        "ComponentVariant",
        "FieldVariant",
        "ComponentState",
        "ComponentSize",
        "TextVariant",
        "Density",
        "ColorToken",
        "ComponentColor",
        "FontToken",
        "WeightToken",
        "RadiusToken",
        "GripToken",
        "DurationToken",
        "EasingToken",
    ];

    static readonly DiagnosticDescriptor TokenError = Descriptors.Error(
        "TRL4001",
        "Design tokens do not compile",
        "Design tokens: {0}"
    );

    static readonly DiagnosticDescriptor UnsetSlot = new(
        "TRL4002",
        "No design token fills a theme slot",
        "Design tokens: {0}",
        "Tralys.Ui",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    const string EmbeddedTokens = "Tralys.Ui.Tokens/";

    // Embedded beside the compiler, so the tokens under the app's always fill the vocabulary it compiles against.
    static readonly Lazy<TokenSet> TralysTokens = new(ReadTralysTokens);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context
            .AdditionalTextsProvider.Where(static f =>
                f.Path.EndsWith(".tokens.json", StringComparison.OrdinalIgnoreCase)
                || f.Path.EndsWith(".resolver.json", StringComparison.OrdinalIgnoreCase)
            )
            .Select(
                static (f, ct) =>
                    new TokenFile(f.Path.Replace('\\', '/'), f.GetText(ct)?.ToString() ?? "")
            )
            .Collect()
            .Select(
                static (all, _) =>
                    new EquatableArray<TokenFile>([
                        .. all.OrderBy(f => f.Path, StringComparer.Ordinal),
                    ])
            );

        var vocabulary = context.CompilationProvider.Select(
            static (compilation, _) => Read(compilation)
        );

        context.RegisterSourceOutput(
            files.Combine(vocabulary).Combine(Output.RootNamespace(context)),
            static (spc, input) => Emit(spc, input.Left.Left, input.Left.Right, input.Right)
        );
    }

    static EquatableArray<VocabularyEnum> Read(Compilation compilation)
    {
        var enums = ImmutableArray.CreateBuilder<VocabularyEnum>(VocabularyNames.Length);
        foreach (var name in VocabularyNames)
        {
            if (
                compilation.GetTypeByMetadataName("Tralys.Ui.Theming." + name)
                is not { TypeKind: TypeKind.Enum } type
            )
                return default;

            enums.Add(new VocabularyEnum(name, new EquatableArray<string>(type.EnumMemberNames())));
        }
        return new EquatableArray<VocabularyEnum>(enums.MoveToImmutable());
    }

    static void Emit(
        SourceProductionContext context,
        EquatableArray<TokenFile> files,
        EquatableArray<VocabularyEnum> vocabulary,
        string ns
    )
    {
        if (files.Items.Length == 0)
            return;

        var resolvers = files
            .Items.Where(f => f.Path.EndsWith(".resolver.json", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (resolvers.Count != 1)
        {
            Fail(
                context,
                files.Items[0].Path,
                resolvers.Count == 0
                    ? "the token files have no *.resolver.json to combine them"
                    : $"{resolvers.Count} *.resolver.json files are given; a project compiles one token set"
            );
            return;
        }

        var resolver = resolvers[0];
        if (vocabulary.Items.Length == 0)
        {
            Fail(
                context,
                resolver.Path,
                "the project does not reference Tralys.Ui, whose vocabulary the tokens fill"
            );
            return;
        }

        var root = resolver.Path.Substring(0, resolver.Path.LastIndexOf('/') + 1);
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files.Items)
        {
            if (!ReferenceEquals(file.Path, resolver.Path))
                sources[RelativeTo(root, file.Path)] = file.Content;
        }

        var errors = new List<string>();
        var warnings = new List<string>();
        var model = ThemeModel.Build(
            new TokenSet(resolver.Content, sources),
            TralysTokens.Value,
            vocabulary.Items.ToDictionary(
                v => v.Name,
                v => v.Members.Items.ToList(),
                StringComparer.Ordinal
            ),
            errors,
            warnings
        );
        foreach (var warning in warnings.Distinct(StringComparer.Ordinal))
            context.ReportDiagnostic(
                Diagnostic.Create(UnsetSlot, Output.AtFile(resolver.Path), warning)
            );
        if (model is null)
        {
            foreach (var error in errors.Distinct(StringComparer.Ordinal))
                Fail(context, resolver.Path, error);
            return;
        }

        Output.Add(context, "Tralys.Ui.Themes.g.cs", Source(model, ns));
    }

    static string RelativeTo(string folder, string path)
    {
        if (path.StartsWith(folder, StringComparison.Ordinal))
            return path.Substring(folder.Length);

        var shared = 0;
        for (var i = 0; i < folder.Length && i < path.Length && folder[i] == path[i]; i++)
        {
            if (folder[i] == '/')
                shared = i + 1;
        }

        var relative = new StringBuilder();
        for (var i = shared; i < folder.Length; i++)
        {
            if (folder[i] == '/')
                relative.Append("../");
        }
        return relative.Append(path, shared, path.Length - shared).ToString();
    }

    static TokenSet ReadTralysTokens()
    {
        var assembly = typeof(ThemeTokenGenerator).Assembly;
        var resolver = "";
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(EmbeddedTokens, StringComparison.Ordinal))
                continue;

            using var stream =
                assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException(
                    $"The listed resource '{name}' does not open."
                );
            using var reader = new StreamReader(stream);
            var path = name.Substring(EmbeddedTokens.Length).Replace('\\', '/');
            if (path.EndsWith(".resolver.json", StringComparison.OrdinalIgnoreCase))
                resolver = reader.ReadToEnd();
            else
                sources[path] = reader.ReadToEnd();
        }
        return new TokenSet(resolver, sources);
    }

    static void Fail(SourceProductionContext context, string path, string message) =>
        context.ReportDiagnostic(Diagnostic.Create(TokenError, Output.AtFile(path), message));

    static string Source(ThemeModel model, string ns)
    {
        var w = new CodeWriter();
        Output.Header(w);
        Output.Namespace(w, ns);

        w.Line("/// <summary>The themes compiled from the app's design tokens.</summary>");
        using (w.Block("public static partial class Themes"))
        {
            foreach (var theme in model.Themes)
            {
                w.Line($"/// <summary>The '{theme.Context}' theme.</summary>");
                w.Line(
                    $"public static {T}.Theme {theme.Member} {{ get; }} = Create{theme.Member}();"
                );
                w.Line();
            }

            var fallback = model.Themes.First(t => t.Context == model.DefaultTheme);
            w.Line("/// <summary>The theme the token resolver defaults to.</summary>");
            w.Line($"public static {T}.Theme Default => {fallback.Member};");
            w.Line();
            w.Line("/// <summary>Every theme, in the resolver's order.</summary>");
            w.Line(
                $"public static global::System.Collections.Generic.IReadOnlyList<{T}.Theme> All {{ get; }} = [{string.Join(", ", model.Themes.Select(t => t.Member))}];"
            );

            foreach (var theme in model.Themes)
            {
                w.Line();
                using (w.Block($"static {T}.Theme Create{theme.Member}()"))
                    Body(w, theme);
            }
        }
        return w.ToString();
    }

    static void Body(CodeWriter w, ThemeData theme)
    {
        w.Line($"var b = new {T}.ThemeBuilder({Literals.Quote(theme.Context)});");
        foreach (var (token, color) in theme.Colors)
            w.Line($"b.Color({T}.ColorToken.{token}, {Color(color)});");
        foreach (var (token, color) in theme.ComponentColors)
            w.Line($"b.Color({T}.ComponentColor.{token}, {Color(color)});");
        foreach (var p in theme.Palettes)
            w.Line(
                $"b.Palette({T}.ColorScheme.{p.Group}, {T}.ComponentVariant.{p.Variant}, {T}.ComponentState.{p.State}, {Color(p.Background)}, {Color(p.Foreground)}, {Color(p.Border)});"
            );
        foreach (var p in theme.Fields)
            w.Line(
                $"b.Field({T}.FieldVariant.{p.Variant}, {T}.ComponentState.{p.State}, {Color(p.Background)}, {Color(p.Foreground)}, {Color(p.Border)});"
            );
        foreach (var (token, family) in theme.Fonts)
            w.Line($"b.Font({T}.FontToken.{token}, {Literals.Quote(family)});");
        foreach (var (token, weight) in theme.Weights)
            w.Line(
                $"b.Weight({T}.WeightToken.{token}, {weight.ToString(CultureInfo.InvariantCulture)});"
            );

        foreach (var density in theme.Densities)
        {
            var d = $"{T}.Density.{density.Member}";
            w.Line($"b.Spacing({d}, {Single(density.Spacing)});");
            foreach (var (size, leaves) in density.Sizes)
                w.Line(
                    $"b.Size({d}, {T}.ComponentSize.{size}, new {T}.ComponentMetrics({string.Join(", ", leaves.Select(l => $"{Camel(l.Leaf)}: {Single(l.Value)}"))}));"
                );
            foreach (var (variant, size) in density.FontSizes)
                w.Line($"b.FontSize({d}, {T}.TextVariant.{variant}, {Single(size)});");
            foreach (var (token, radius) in density.Radii)
                w.Line($"b.Radius({d}, {T}.RadiusToken.{token}, {Single(radius)});");
            foreach (var (token, size) in density.Grips)
                w.Line($"b.Grip({d}, {T}.GripToken.{token}, {Single(size)});");
        }

        foreach (var (token, seconds) in theme.Durations)
            w.Line($"b.Duration({T}.DurationToken.{token}, {Single(seconds)});");
        foreach (var (token, curve) in theme.Easings)
            w.Line(
                $"b.Easing({T}.EasingToken.{token}, new global::Tralys.Ui.KeySpline({string.Join(", ", curve.Select(Single))}));"
            );
        w.Line("return b.Build();");
    }

    static string Color(Argb c) => $"global::Tralys.Ui.Color.FromArgb({c.A}, {c.R}, {c.G}, {c.B})";

    static string Single(double value) => Literals.Single((float)value);

    static string Camel(string kebab)
    {
        var pascal = ThemeModel.Pascal(kebab);
        return char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
    }
}
