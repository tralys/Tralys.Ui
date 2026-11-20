using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Generators.Stories;

/// <summary>Builds the assembly's <c>StoryRegistry</c>: each <c>public static void Name(Ui ui)</c> of a class marked
/// <c>[Stories&lt;T&gt;(category)]</c> registers as <c>T.Name</c> under that category, T's name joining its containing
/// types' with dots. A key registered twice is an error at each of its stories. Each story carries its method's doc
/// comment summary and, under the <c>TralysStoryRoot</c> build property, its file and line.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class StoryRegistryGenerator : IIncrementalGenerator
{
    static readonly DiagnosticDescriptor DuplicateKey = Descriptors.Error(
        "TRL3013",
        "Two stories share a key",
        "The story key '{0}' is registered more than once; rename all but one of its stories"
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var classes = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                UiShapes.Stories,
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => Read(ctx, ct)
            )
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        var root = context.AnalyzerConfigOptionsProvider.Select(
            static (options, _) =>
                options.GlobalOptions.TryGetValue("build_property.TralysStoryRoot", out var value)
                    ? value
                    : ""
        );

        context.RegisterSourceOutput(
            classes.Combine(Output.RootNamespace(context)).Combine(root),
            static (spc, input) => Emit(spc, input.Left.Left, input.Left.Right, input.Right)
        );
    }

    static StoriesClass? Read(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var type = (INamedTypeSymbol)ctx.TargetSymbol;
        if (
            ctx.SemanticModel.Compilation.GetTypeByMetadataName(UiShapes.Ui) is not { } ui
            || UiShapes.ReachOf(type) == UiShapes.Reach.None
            || ctx.Attributes.FirstOrDefault() is not { } attribute
            || attribute.AttributeClass?.TypeArguments.FirstOrDefault()
                is not INamedTypeSymbol subject
            || subject.IsUnboundGenericType
        )
            return null;

        var stories = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m =>
                m
                    is {
                        MethodKind: MethodKind.Ordinary,
                        IsStatic: true,
                        IsGenericMethod: false,
                        DeclaredAccessibility: Accessibility.Public,
                        ReturnsVoid: true,
                        Parameters.Length: 1,
                    }
                && m.Parameters[0].RefKind == RefKind.None
                && SymbolEqualityComparer.Default.Equals(m.Parameters[0].Type, ui)
            )
            .Select(m => new StoryMethod(
                Literals.Identifier(m.Name),
                m.Name,
                Summary(m.GetDocumentationCommentXml(cancellationToken: ct)),
                LocationInfo.From(m.Locations.FirstOrDefault())
            ));

        return new StoriesClass(
            type.Fq(),
            subject.Fq(),
            UiShapes.Chain(subject, "."),
            attribute.ConstructorArguments.FirstOrDefault().Value as string ?? "",
            new([.. stories])
        );
    }

    static void Emit(
        SourceProductionContext context,
        ImmutableArray<StoriesClass> classes,
        string ns,
        string root
    )
    {
        if (classes.IsEmpty)
            return;

        var stories = classes
            .SelectMany(c =>
                c.Stories.Items.Select(s => (Key: c.Prefix + "." + s.Name, Class: c, Story: s))
            )
            .ToList();
        var counts = stories
            .GroupBy(s => s.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var story in stories.Where(s => counts[s.Key] > 1))
            context.ReportDiagnostic(
                Diagnostic.Create(DuplicateKey, story.Story.Location?.ToLocation(), story.Key)
            );

        var w = new CodeWriter();
        Output.Header(w);
        Output.Namespace(w, ns);
        w.Line(
            "[global::System.CodeDom.Compiler.GeneratedCode(\"Tralys.Ui.Generators\", \"1.0\")]"
        );
        using (w.Block("public static class StoryRegistry"))
        {
            w.Line("/// <summary>Every story in this assembly, ordered by key.</summary>");
            w.Line(
                "public static global::System.Collections.Generic.IReadOnlyList<global::Tralys.Ui.Story> All { get; } ="
            );
            w.Line("[");
            using (w.Indented())
            {
                foreach (
                    var story in stories
                        .Where(s => counts[s.Key] == 1)
                        .OrderBy(s => s.Key, StringComparer.Ordinal)
                )
                    w.Line(
                        $"new({Literals.Quote(story.Key)}, {Literals.Quote(story.Class.Category)}, typeof({story.Class.Subject}), {story.Class.Type}.{story.Story.Member}){Initializer(story.Story, root)},"
                    );
            }
            w.Line("];");
        }
        Output.Add(context, "StoryRegistry.g.cs", w.ToString());
    }

    static string Initializer(StoryMethod story, string root)
    {
        var members = new List<string>(3);
        if (story.Description is { } description)
            members.Add("Description = " + Literals.Quote(description));
        if (story.Location is { } location && Relative(location.FilePath, root) is { } path)
        {
            members.Add("SourcePath = " + Literals.Quote(path));
            members.Add(
                "SourceLine = "
                    + (location.LineSpan.Start.Line + 1).ToString(CultureInfo.InvariantCulture)
            );
        }
        return members.Count == 0 ? "" : " { " + string.Join(", ", members) + " }";
    }

    static string? Relative(string path, string root)
    {
        if (root.Length == 0)
            return null;
        var file = path.Replace('\\', '/');
        var under = root.Replace('\\', '/');
        if (!under.EndsWith("/", StringComparison.Ordinal))
            under += "/";
        return file.StartsWith(under, StringComparison.OrdinalIgnoreCase)
            ? file.Substring(under.Length)
            : null;
    }

    static string? Summary(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            return null;
        XElement? summary;
        try
        {
            summary = XElement.Parse(xml).Element("summary");
        }
        catch (XmlException)
        {
            return null;
        }
        if (summary is null)
            return null;
        var text = new StringBuilder();
        Flatten(summary, text);
        var plain = string.Join(
            " ",
            text.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        );
        return plain.Length == 0 ? null : plain;
    }

    static void Flatten(XElement element, StringBuilder text)
    {
        foreach (var node in element.Nodes())
        {
            if (node is XText run)
                text.Append(run.Value);
            else if (node is not XElement inner)
                continue;
            else if (!inner.IsEmpty)
                Flatten(inner, text);
            else if (inner.Attribute("cref") is { } cref)
                text.Append(Named(cref.Value));
            else if ((inner.Attribute("langword") ?? inner.Attribute("name")) is { } word)
                text.Append(word.Value);
        }
    }

    static string Named(string cref)
    {
        var name = cref.Length > 1 && cref[1] == ':' ? cref.Substring(2) : cref;
        var call = name.IndexOf('(');
        if (call >= 0)
            name = name.Substring(0, call);
        name = name.Substring(name.LastIndexOf('.') + 1);
        var arity = name.IndexOf('`');
        return arity >= 0 ? name.Substring(0, arity) : name;
    }

    readonly record struct StoryMethod(
        string Member,
        string Name,
        string? Description,
        LocationInfo? Location
    );

    readonly record struct StoriesClass(
        string Type,
        string Subject,
        string Prefix,
        string Category,
        EquatableArray<StoryMethod> Stories
    );
}
