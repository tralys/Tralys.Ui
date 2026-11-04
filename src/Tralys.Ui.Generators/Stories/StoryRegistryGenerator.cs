using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Generators.Stories;

/// <summary>Builds the assembly's <c>StoryRegistry</c>: each <c>public static void Name(Ui ui)</c> of a class marked
/// <c>[Stories&lt;T&gt;(category)]</c> registers as <c>T.Name</c> under that category, T's name joining its containing
/// types' with dots. A key registered twice is an error at each of its stories.</summary>
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
                static (ctx, _) => Read(ctx)
            )
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value)
            .Collect();

        context.RegisterSourceOutput(
            classes.Combine(Output.RootNamespace(context)),
            static (spc, input) => Emit(spc, input.Left, input.Right)
        );
    }

    static StoriesClass? Read(GeneratorAttributeSyntaxContext ctx)
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
        string ns
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
                        $"new({Literals.Quote(story.Key)}, {Literals.Quote(story.Class.Category)}, typeof({story.Class.Subject}), {story.Class.Type}.{story.Story.Member}),"
                    );
            }
            w.Line("];");
        }
        Output.Add(context, "StoryRegistry.g.cs", w.ToString());
    }

    readonly record struct StoryMethod(string Member, string Name, LocationInfo? Location);

    readonly record struct StoriesClass(
        string Type,
        string Subject,
        string Prefix,
        string Category,
        EquatableArray<StoryMethod> Stories
    );
}
