using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Tralys.Ui.Generators.Components;
using Tralys.Ui.Generators.Stories;

namespace Tralys.Ui.Generators.Tests;

static class Harness
{
    static readonly MetadataReference[] References =
    [
        .. File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "harness.references"))
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    static readonly CSharpParseOptions Parse = new(LanguageVersion.Preview);

    static readonly CSharpCompilationOptions Options = new(
        OutputKind.DynamicallyLinkedLibrary,
        nullableContextOptions: NullableContextOptions.Enable
    );

    public static Compilation Generate(
        string source,
        out ImmutableArray<Diagnostic> reported,
        NullableContextOptions nullable = NullableContextOptions.Enable
    )
    {
        CSharpGeneratorDriver
            .Create(
                [
                    new ComponentCallGenerator().AsSourceGenerator(),
                    new StoryRegistryGenerator().AsSourceGenerator(),
                ],
                parseOptions: Parse
            )
            .RunGeneratorsAndUpdateCompilation(
                Compile(source).WithOptions(Options.WithNullableContextOptions(nullable)),
                out var output,
                out reported
            );
        return output;
    }

    /// <summary>What <paramref name="generator"/> reports on <paramref name="source"/>, and the errors of the
    /// compilation it leaves.</summary>
    public static ImmutableArray<Diagnostic> Diagnostics(
        IIncrementalGenerator generator,
        string source,
        ImmutableArray<AdditionalText> additionalTexts = default,
        IReadOnlyDictionary<string, string>? buildProperties = null
    )
    {
        CSharpGeneratorDriver
            .Create(
                [generator.AsSourceGenerator()],
                additionalTexts.IsDefault ? [] : additionalTexts,
                Parse,
                buildProperties is null ? null : new BuildProperties(buildProperties)
            )
            .RunGeneratorsAndUpdateCompilation(Compile(source), out var output, out var reported);

        return [.. reported, .. output.GetDiagnostics()];
    }

    public static System.Reflection.Assembly Load(
        IIncrementalGenerator generator,
        string source,
        ImmutableArray<AdditionalText> additionalTexts = default,
        IReadOnlyDictionary<string, string>? buildProperties = null,
        string path = "Sample.cs"
    )
    {
        CSharpGeneratorDriver
            .Create(
                [generator.AsSourceGenerator()],
                additionalTexts.IsDefault ? [] : additionalTexts,
                Parse,
                buildProperties is null ? null : new BuildProperties(buildProperties)
            )
            .RunGeneratorsAndUpdateCompilation(Compile((path, source)), out var output, out _);

        using var image = new MemoryStream();
        var emitted = output.Emit(image);
        if (!emitted.Success)
            throw new InvalidOperationException(
                string.Join(
                    "\n",
                    emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                )
            );
        return System.Reflection.Assembly.Load(image.ToArray());
    }

    public static CSharpCompilation Compile(string source) => Compile(("Sample.cs", source));

    public static CSharpCompilation Compile(params (string Path, string Source)[] files) =>
        CSharpCompilation.Create(
            "Sample",
            files.Select(file => CSharpSyntaxTree.ParseText(file.Source, Parse, file.Path)),
            References,
            Options
        );

    public static SyntaxTree Edited(SyntaxTree tree, string source) =>
        tree.WithChangedText(SourceText.From(source));

    public static GeneratorDriver Tracking(params IIncrementalGenerator[] generators) =>
        CSharpGeneratorDriver.Create(
            [.. generators.Select(GeneratorExtensions.AsSourceGenerator)],
            parseOptions: Parse,
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );

    // The generators run first, so a source may call the components it declares.
    public static async Task<IEnumerable<(string Id, int Line)>> Analyze(
        DiagnosticAnalyzer analyzer,
        string source
    ) => (await Reported(analyzer, source)).Select(Located);

    public static async Task<ImmutableArray<Diagnostic>> Reported(
        DiagnosticAnalyzer analyzer,
        string source
    )
    {
        var compilation = Generate(source, out _);
        var broken = compilation
            .GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (broken.Length > 0)
            throw new InvalidOperationException(
                string.Join("\n", broken.Select(d => d.ToString()))
            );

        return await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();
    }

    public static async Task<string> Fix(
        DiagnosticAnalyzer analyzer,
        CodeFixProvider fix,
        string source
    )
    {
        using var workspace = new AdhocWorkspace();
        var project = workspace.AddProject(
            ProjectInfo.Create(
                ProjectId.CreateNewId(),
                VersionStamp.Default,
                "Sample",
                "Sample",
                LanguageNames.CSharp,
                compilationOptions: Options,
                parseOptions: Parse,
                metadataReferences: References
            )
        );
        var document = workspace.AddDocument(project.Id, "Sample.cs", SourceText.From(source));
        var compilation = await Assert
            .That(await document.Project.GetCompilationAsync())
            .IsNotNull();
        var diagnostic = (
            await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync()
        ).Single();

        var actions = new List<CodeAction>();
        await fix.RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), default)
        );
        var operations = await actions.Single().GetOperationsAsync(default);
        var fixedDocument = await Assert
            .That(
                operations
                    .OfType<ApplyChangesOperation>()
                    .Single()
                    .ChangedSolution.GetDocument(document.Id)
            )
            .IsNotNull();
        return (await fixedDocument.GetTextAsync()).ToString();
    }

    public static int LineOf(string source, string text) =>
        Array.FindIndex(source.Split('\n'), line => line.Contains(text, StringComparison.Ordinal));

    public static (string Id, int Line) Located(this Diagnostic diagnostic) =>
        (diagnostic.Id, diagnostic.Location.GetLineSpan().StartLinePosition.Line);

    public sealed class Text(string path, string text) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(text);
    }

    sealed class BuildProperties(IReadOnlyDictionary<string, string> properties)
        : AnalyzerConfigOptionsProvider
    {
        static readonly ConfigOptions None = new(new Dictionary<string, string>());

        public override AnalyzerConfigOptions GlobalOptions { get; } =
            new ConfigOptions(
                properties.ToDictionary(p => "build_property." + p.Key, p => p.Value)
            );

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => None;
    }

    sealed class ConfigOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            values.TryGetValue(key, out value);
    }
}
