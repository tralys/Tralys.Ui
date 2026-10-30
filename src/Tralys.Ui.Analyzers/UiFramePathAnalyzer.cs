using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

/// <summary>Requires every method and local function that takes a <c>Ui</c>, stories included, to be on the frame path
/// through <c>[FramePath]</c> on it, on a member it overrides or implements, or on its type (TRL3005).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UiFramePathAnalyzer : DiagnosticAnalyzer
{
    public const string Id = "TRL3005";

    static readonly DiagnosticDescriptor Unmarked = Descriptors.Error(
        Id,
        "A method that takes a Ui is not marked [FramePath]",
        "'{0}' takes a Ui, so it runs every frame; mark it [FramePath]"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Unmarked];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (UiCore.From(start.Compilation) is not { } core)
                return;
            start.RegisterSymbolAction(
                c => Analyze(c.ReportDiagnostic, core, (IMethodSymbol)c.Symbol),
                SymbolKind.Method
            );
            start.RegisterOperationAction(
                c =>
                    Analyze(
                        c.ReportDiagnostic,
                        core,
                        ((ILocalFunctionOperation)c.Operation).Symbol
                    ),
                OperationKind.LocalFunction
            );
        });
    }

    static void Analyze(Action<Diagnostic> report, UiCore core, IMethodSymbol method)
    {
        if (
            method.IsImplicitlyDeclared
            || method.MethodKind
                is not (MethodKind.Ordinary or MethodKind.Constructor or MethodKind.LocalFunction)
            || !core.TakesUi(method)
            || FramePaths.OnFramePath(method)
        )
            return;
        report(Diagnostic.Create(Unmarked, method.Locations.FirstOrDefault(), method.Name));
    }
}
