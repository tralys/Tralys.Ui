using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

/// <summary>Checks the types hooks keep. A <c>State&lt;T&gt;</c>, a canvas's or custom layout's state and a
/// <c>Remember</c> or <c>Effect</c> dep are compared on every render, so each is <c>IEquatable&lt;T&gt;</c>, an enum
/// or a class (TRL3012). A <c>ui.Remember</c> in a row slot's render keeps an <c>IResettable</c>, which a recycled row resets
/// instead of rebuilding (TRL3011, a warning).</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HookArgumentAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Unresettable = new(
        "TRL3011",
        "A row slot remembers what it can't reset",
        "'{0}' isn't IResettable, so a recycled row disposes and rebuilds it, which allocates",
        "Tralys.Ui",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor Unequatable = Descriptors.Error(
        "TRL3012",
        "A hook keeps a value it can only compare by boxing",
        "'{0}' is compared on every render; make it IEquatable<{0}>, an enum or a class, since comparing it otherwise boxes"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [Unresettable, Unequatable];

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
        if (!core.OnUi(invocation) || method.TypeArguments.IsEmpty)
            return;

        var compared = (method.Name, method.IsExtensionMethod, method.TypeArguments.Length) switch
        {
            ("State", false, 1) => method.TypeArguments[0],
            ("Canvas", true, 1) => method.TypeArguments[0],
            ("Layout", true, 1) => method.TypeArguments[0],
            ("Remember", false, 2) => method.TypeArguments[0],
            ("Effect", false, 2) => method.TypeArguments[1],
            _ => null,
        };
        if (compared is not null && !Comparable(compared, core))
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Unequatable,
                    invocation.Syntax.GetLocation(),
                    compared.ToDisplayString()
                )
            );

        if (
            method is { Name: "Remember", IsExtensionMethod: false }
            && method.TypeArguments[method.TypeArguments.Length - 1] is { } kept
            && kept is not ITypeParameterSymbol
            && !Resettable(kept, core)
            && InRowSlot(invocation, core)
        )
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Unresettable,
                    invocation.Syntax.GetLocation(),
                    kept.ToDisplayString()
                )
            );
    }

    static bool Comparable(ITypeSymbol type, UiCore core) =>
        type.IsReferenceType
        || type is ITypeParameterSymbol
        || type.TypeKind == TypeKind.Enum
        || (
            type.TupleElementTypes() is { } elements
                ? elements.All(e => Comparable(e, core))
                : core.IsEquatable(type)
        )
        || type.NullableOf() is { } inner && Comparable(inner, core);

    static bool Resettable(ITypeSymbol type, UiCore core) =>
        core.Resettable is null
        || SymbolEqualityComparer.Default.Equals(type, core.Resettable)
        || type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, core.Resettable));

    // A row slot is the lambda a Slot<TItem> is made of; a lambda nested inside it renders elsewhere.
    static bool InRowSlot(IOperation operation, UiCore core)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is ILocalFunctionOperation)
                return false;
            if (parent is not IAnonymousFunctionOperation lambda)
                continue;
            var use = lambda.Parent?.Parent;
            while (use is IConversionOperation)
                use = use.Parent;
            return use is IArgumentOperation { Parent: IInvocationOperation of }
                && of.TargetMethod.Name == "Of"
                && SymbolEqualityComparer.Default.Equals(
                    of.TargetMethod.ContainingType.OriginalDefinition,
                    core.Slot
                );
        }
        return false;
    }
}
