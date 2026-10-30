using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tralys.Ui.Analyzers;

/// <summary>Flags a collection that raises the BCL <c>INotifyCollectionChanged</c>, whose event args allocate on
/// every change. Tralys.Ui accepts one as an items source, but a collection with struct change events allocates
/// nothing.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionChangedAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Rule = new(
        "TRL2101",
        "BCL collection change notification",
        "'{0}' raises INotifyCollectionChanged, which allocates its event args on every change; prefer a collection with struct change events",
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
                start.Compilation.GetTypeByMetadataName(
                    "System.Collections.Specialized.INotifyCollectionChanged"
                )
                is not { } notifies
            )
                return;

            start.RegisterOperationAction(
                c =>
                {
                    if (
                        c.Operation.Type is { } created
                        && created.AllInterfaces.Contains(notifies, SymbolEqualityComparer.Default)
                    )
                        c.ReportDiagnostic(
                            Diagnostic.Create(
                                Rule,
                                c.Operation.Syntax.GetLocation(),
                                created.ToDisplayString()
                            )
                        );
                },
                OperationKind.ObjectCreation,
                OperationKind.CollectionExpression
            );

            start.RegisterSymbolAction(
                c =>
                {
                    var type = (INamedTypeSymbol)c.Symbol;
                    if (type.Interfaces.Contains(notifies, SymbolEqualityComparer.Default))
                        c.ReportDiagnostic(
                            Diagnostic.Create(
                                Rule,
                                type.Locations.FirstOrDefault(),
                                type.ToDisplayString()
                            )
                        );
                },
                SymbolKind.NamedType
            );
        });
    }
}
