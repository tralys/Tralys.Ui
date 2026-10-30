using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

/// <summary>Requires a call that opens a <c>Scope</c> to be the resource of a <c>using</c>, or the value its method
/// returns to a caller that is held to the same rule; a scope left open would take every later call as its child
/// (TRL3010).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ScopeAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Unclosed = Descriptors.Error(
        "TRL3010",
        "A Scope is not the resource of a using",
        "'{0}' opens a Scope; make it the resource of a using, so its block closes it"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Unclosed];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (UiCore.From(start.Compilation) is not { Scope: not null } core)
                return;
            start.RegisterOperationAction(c => Analyze(c, core), OperationKind.Invocation);
        });
    }

    static void Analyze(OperationAnalysisContext context, UiCore core)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!core.ReturnsScope(invocation.TargetMethod) || Closed(invocation))
            return;
        context.ReportDiagnostic(
            Diagnostic.Create(
                Unclosed,
                invocation.Syntax.GetLocation(),
                invocation.TargetMethod.Name
            )
        );
    }

    static bool Closed(IOperation operation)
    {
        var parent = operation.Parent;
        while (parent is IConversionOperation or IConditionalOperation or ICoalesceOperation)
            parent = parent.Parent;
        while (
            parent
                is IVariableInitializerOperation
                    or IVariableDeclaratorOperation
                    or IVariableDeclarationOperation
                    or IVariableDeclarationGroupOperation
        )
            parent = parent.Parent;
        return parent is IUsingOperation or IUsingDeclarationOperation or IReturnOperation;
    }
}
