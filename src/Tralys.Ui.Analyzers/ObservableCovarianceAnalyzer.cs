using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Tralys.Ui.Analyzers;

/// <summary>Flags an observable collection of one element type seen as an <c>IReadOnlyList</c> of another, such as an
/// <c>ObservableList&lt;Dog&gt;</c> as an <c>IReadOnlyList&lt;IAnimal&gt;</c>. An items source made from it reads the
/// rows but never hears them change, since its change events name the element type it was made with.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ObservableCovarianceAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Rule = new(
        "TRL2102",
        "Observable collection seen through a wider element type",
        "'{0}' seen as '{1}' is never heard changing by a list showing it; expose it as 'IReadOnlyList<{2}>'",
        "Tralys.Ui",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (
                start.Compilation.GetTypeByMetadataName("Tralys.Ui.Mvvm.ObservableList`1")
                    is not { } tracked
                || start.Compilation.GetTypeByMetadataName(
                    "System.Collections.Generic.IReadOnlyList`1"
                )
                    is not { } readOnlyList
            )
                return;

            start.RegisterOperationAction(
                c =>
                {
                    var conversion = (IConversionOperation)c.Operation;
                    if (
                        conversion.Type is not INamedTypeSymbol { IsGenericType: true } target
                        || !SymbolEqualityComparer.Default.Equals(
                            target.OriginalDefinition,
                            readOnlyList
                        )
                        || conversion.Operand.Type is not { } source
                        || ElementOf(source, tracked) is not { } element
                        || SymbolEqualityComparer.Default.Equals(element, target.TypeArguments[0])
                    )
                        return;

                    c.ReportDiagnostic(
                        Diagnostic.Create(
                            Rule,
                            conversion.Operand.Syntax.GetLocation(),
                            source.ToDisplayString(),
                            target.ToDisplayString(),
                            element.ToDisplayString()
                        )
                    );
                },
                OperationKind.Conversion
            );
        });
    }

    static ITypeSymbol? ElementOf(ITypeSymbol type, INamedTypeSymbol tracked) =>
        type is INamedTypeSymbol named
        && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, tracked)
            ? named.TypeArguments[0]
            : null;
}
