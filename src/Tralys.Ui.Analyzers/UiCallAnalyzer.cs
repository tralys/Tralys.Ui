using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

/// <summary>Checks the arguments of generated <c>ui.*</c> calls, components' and primitives': each is named, except a
/// call's one required argument when it comes first (TRL3007); and the outermost call of a component's
/// <c>Render</c> doesn't place it, since placement belongs to the component's caller (TRL3008).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UiCallAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Unnamed = Descriptors.Error(
        "TRL3007",
        "A ui call argument is not named",
        "Name this argument '{0}:'; only a call's one required argument, when it comes first, may go unnamed"
    );

    static readonly DiagnosticDescriptor PlacedRoot = Descriptors.Error(
        "TRL3008",
        "A component places its own root",
        "'{0}:' places '{1}''s root, but placement belongs to the component's caller"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [Unnamed, PlacedRoot];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (UiCore.From(start.Compilation) is not { } core)
                return;
            start.RegisterOperationAction(c => Analyze(c, core), OperationKind.Invocation);
        });
    }

    static void Analyze(OperationAnalysisContext context, UiCore core)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!core.IsGeneratedShape(method))
            return;

        var content = ContentParameter(method);
        foreach (var argument in invocation.Arguments)
        {
            if (
                argument is { ArgumentKind: ArgumentKind.Explicit, Parameter: { } parameter }
                && argument.Syntax is ArgumentSyntax { NameColon: null }
                && parameter.Ordinal != 0
                && parameter.Ordinal != content
            )
                context.ReportDiagnostic(
                    Diagnostic.Create(Unnamed, argument.Syntax.GetLocation(), parameter.Name)
                );
        }

        if (
            context.ContainingSymbol is IMethodSymbol render
            && core.IsRender(render)
            && Outermost(invocation, core)
        )
        {
            foreach (var argument in invocation.Arguments)
            {
                if (
                    argument is { ArgumentKind: ArgumentKind.Explicit, Parameter: { } parameter }
                    && core.IsPlacement(parameter)
                )
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            PlacedRoot,
                            argument.Syntax.GetLocation(),
                            parameter.Name,
                            render.ContainingType.Name
                        )
                    );
            }
        }
    }

    static int ContentParameter(IMethodSymbol method)
    {
        var required = method
            .Parameters.Skip(1)
            .Where(p => !p.HasExplicitDefaultValue && !p.IsParams)
            .ToList();
        return required.Count == 1 && required[0].Ordinal == 1 ? 1 : -1;
    }

    // The first top-level call in source order; the host's Debug assert catches a root a branch picks.
    static bool Outermost(IInvocationOperation invocation, UiCore core)
    {
        if (!TopLevel(invocation, core))
            return false;
        var body = (IOperation)invocation;
        while (body.Parent is { } parent)
            body = parent;
        var start = invocation.Syntax.SpanStart;
        return !body.Descendants()
            .OfType<IInvocationOperation>()
            .Any(other =>
                other.Syntax.SpanStart < start
                && core.IsGeneratedShape(other.TargetMethod)
                && TopLevel(other, core)
            );
    }

    static bool TopLevel(IInvocationOperation invocation, UiCore core) =>
        core.ScopesAround(invocation, out var inLambda).All(s => core.IsTransparent(s.TargetMethod))
        && !inLambda;
}
