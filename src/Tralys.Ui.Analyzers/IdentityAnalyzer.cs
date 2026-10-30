using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

/// <summary>Keeps call-site identity apart. A method that takes a <c>Ui</c> and makes identity-carrying calls forwards
/// its caller's file and line to its one top-level call, or wraps its body in a <c>ui.Fragment</c> that does (TRL3003);
/// <c>Render</c> and registered stories are exempt. A node, component, fragment or hook call in a loop body passes
/// <c>key:</c> unless a scope inside the loop already tells its iterations apart (TRL3004).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class IdentityAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Unforwarded = Descriptors.Error(
        "TRL3003",
        "A ui helper does not forward its caller's identity",
        "'{0}' {1}, so every place that calls it shares one identity"
    );

    static readonly DiagnosticDescriptor Unkeyed = Descriptors.Error(
        "TRL3004",
        "An identity-carrying ui call in a loop has no key",
        "'{0}' runs in a loop without key:, so its iterations share one identity; pass key:, or wrap it in a keyed ui.Fragment"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [Unforwarded, Unkeyed];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (UiCore.From(start.Compilation) is not { } core)
                return;
            start.RegisterOperationAction(c => Loop(c, core), OperationKind.Invocation);
            start.RegisterOperationAction(
                c =>
                {
                    if (c.ContainingSymbol is IMethodSymbol method)
                        Helper(c, core, method, c.Operation);
                },
                OperationKind.MethodBody
            );
            start.RegisterOperationAction(
                c =>
                {
                    var local = (ILocalFunctionOperation)c.Operation;
                    if (local.Body is { } body)
                        Helper(c, core, local.Symbol, body);
                },
                OperationKind.LocalFunction
            );
        });
    }

    static void Loop(OperationAnalysisContext context, UiCore core)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (
            core.CarriesIdentity(invocation)
            && !core.HasKey(invocation)
            && core.LoopAround(invocation, core.IsHook(invocation.TargetMethod)) is not null
        )
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Unkeyed,
                    invocation.Syntax.GetLocation(),
                    invocation.TargetMethod.Name
                )
            );
    }

    static void Helper(
        OperationAnalysisContext context,
        UiCore core,
        IMethodSymbol method,
        IOperation body
    )
    {
        if (!core.TakesUi(method) || core.IsRender(method) || core.IsStory(method))
            return;

        var calls = new List<IInvocationOperation>();
        Collect(body, core, calls);
        if (calls.Count == 0)
            return;

        var file = UiCore.FileOf(method);
        var line = UiCore.LineOf(method);
        if (file is null || line is null)
        {
            Report(
                context,
                method,
                "makes ui calls but declares no [CallerFilePath] string file and [CallerLineNumber] int line to forward"
            );
            return;
        }

        var top = calls.Where(c => core.ScopesAround(c, out _).Count == 0).ToList();
        if (top.Count == 1 && Forwards(top[0], file, line))
        {
            var root = top[0];
            var fragment = core.IsFragment(root.TargetMethod);
            if (
                calls.All(c =>
                    c == root
                    || !core.IsHook(c.TargetMethod)
                    || fragment && core.ScopesAround(c, out _).Contains(root)
                )
            )
                return;
        }
        Report(
            context,
            method,
            "must forward file and line to its one top-level ui call, or wrap its body in ui.Fragment(file: file, line: line)"
        );
    }

    static void Collect(IOperation operation, UiCore core, List<IInvocationOperation> calls)
    {
        foreach (var child in operation.ChildOperations)
        {
            if (child is IAnonymousFunctionOperation or ILocalFunctionOperation)
                continue;
            if (child is IInvocationOperation invocation && core.CarriesIdentity(invocation))
                calls.Add(invocation);
            Collect(child, core, calls);
        }
    }

    static bool Forwards(IInvocationOperation call, IParameterSymbol file, IParameterSymbol line) =>
        Passes(call, UiCore.FileOf(call.TargetMethod), file)
        && Passes(call, UiCore.LineOf(call.TargetMethod), line);

    static bool Passes(IInvocationOperation call, IParameterSymbol? target, IParameterSymbol own)
    {
        foreach (var argument in call.Arguments)
        {
            if (
                argument.ArgumentKind != ArgumentKind.DefaultValue
                && SymbolEqualityComparer.Default.Equals(
                    argument.Parameter?.OriginalDefinition,
                    target?.OriginalDefinition
                )
            )
            {
                var value = argument.Value;
                while (value is IConversionOperation conversion)
                    value = conversion.Operand;
                return value is IParameterReferenceOperation reference
                    && SymbolEqualityComparer.Default.Equals(reference.Parameter, own);
            }
        }
        return false;
    }

    static void Report(OperationAnalysisContext context, IMethodSymbol method, string problem) =>
        context.ReportDiagnostic(
            Diagnostic.Create(Unforwarded, method.Locations.FirstOrDefault(), method.Name, problem)
        );
}
