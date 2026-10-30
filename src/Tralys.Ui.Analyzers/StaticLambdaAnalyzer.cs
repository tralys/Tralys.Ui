using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

/// <summary>Requires a lambda that carries the frame path, one converted to a <c>[FramePath]</c> delegate type
/// (<c>CanvasDraw</c>, <c>EffectFn</c>, <c>RefAction</c>, ...) or passed to a <c>[FramePath]</c> parameter
/// (<c>Callback.Of</c>, <c>Slot.Of</c>, ...), to be <c>static</c>: the compiler caches it then, and two callbacks
/// holding it compare equal (TRL3002).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StaticLambdaAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor NotStatic = Descriptors.Error(
        "TRL3002",
        "A callback, handler, slot or draw lambda is not static",
        "Make this lambda static and pass what it needs as its state; callbacks compare by their delegate, which a lambda that isn't static may make anew"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [NotStatic];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Analyze, OperationKind.AnonymousFunction);
    }

    static void Analyze(OperationAnalysisContext context)
    {
        var lambda = (IAnonymousFunctionOperation)context.Operation;
        if (!lambda.Symbol.IsStatic && FramePaths.IsCarrier(lambda))
            context.ReportDiagnostic(Diagnostic.Create(NotStatic, lambda.Syntax.GetLocation()));
    }
}
