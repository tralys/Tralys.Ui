using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

/// <summary>Bans what allocates on a frame path from members marked <c>[Tralys.Ui.FramePath]</c>, from every override
/// and interface implementation of one, from their accessors, from every member of a type so marked, and from every
/// lambda converted to a marked delegate type or passed to a marked parameter: LINQ, iterators and allocating
/// enumerators, <c>params</c> arrays, string concatenation, capturing delegates and boxing.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FramePathAnalyzer : DiagnosticAnalyzer
{
    static readonly DiagnosticDescriptor Linq = Descriptors.Error(
        "TRL2001",
        "LINQ on a frame path",
        "'{0}' is LINQ, which allocates its iterators and delegates; loop instead"
    );

    static readonly DiagnosticDescriptor Iterator = Descriptors.Error(
        "TRL2002",
        "Iterator on a frame path",
        "{0}; index the collection or enumerate a struct enumerator instead"
    );

    static readonly DiagnosticDescriptor Params = Descriptors.Error(
        "TRL2003",
        "params array on a frame path",
        "Calling '{0}' allocates its params array; pass a span or the values one by one"
    );

    static readonly DiagnosticDescriptor Concat = Descriptors.Error(
        "TRL2004",
        "String concatenation on a frame path",
        "{0} allocates a string; format into a reused buffer instead"
    );

    static readonly DiagnosticDescriptor Capture = Descriptors.Error(
        "TRL2005",
        "Capturing delegate on a frame path",
        "{0} allocates a delegate each time it runs; make it static and pass its state"
    );

    static readonly DiagnosticDescriptor Boxing = Descriptors.Error(
        "TRL2006",
        "Boxing on a frame path",
        "Converting '{0}' to '{1}' boxes it"
    );

    static readonly OperationKind[] Checked =
    [
        OperationKind.Invocation,
        OperationKind.ObjectCreation,
        OperationKind.Binary,
        OperationKind.CompoundAssignment,
        OperationKind.InterpolatedString,
        OperationKind.AnonymousFunction,
        OperationKind.DelegateCreation,
        OperationKind.Conversion,
        OperationKind.YieldReturn,
        OperationKind.Loop,
    ];

    internal static readonly ImmutableArray<DiagnosticDescriptor> Rules =
    [
        Linq,
        Iterator,
        Params,
        Concat,
        Capture,
        Boxing,
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Rules;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationBlockStartAction(Start);
    }

    static void Start(OperationBlockStartAnalysisContext context)
    {
        if (FramePaths.OnFramePath(context.OwningSymbol))
            context.RegisterOperationAction(c => Check(c.Operation, c.ReportDiagnostic), Checked);
        else
            context.RegisterOperationAction(Carrier, OperationKind.AnonymousFunction);
    }

    static void Carrier(OperationAnalysisContext context)
    {
        var lambda = (IAnonymousFunctionOperation)context.Operation;
        if (!FramePaths.IsCarrier(lambda))
            return;
        for (var outer = lambda.Parent; outer is not null; outer = outer.Parent)
        {
            if (outer is IAnonymousFunctionOperation enclosing && FramePaths.IsCarrier(enclosing))
                return;
        }
        foreach (var operation in lambda.DescendantsAndSelf())
            Check(operation, context.ReportDiagnostic);
    }

    internal static void Check(IOperation operation, Action<Diagnostic> report)
    {
        switch (operation)
        {
            case IInvocationOperation invocation:
                Invocation(invocation, report);
                break;
            case IObjectCreationOperation creation:
                Arguments(creation, creation.Arguments, ".ctor", report);
                break;
            case IBinaryOperation binary:
                if (IsConcatenation(binary) && !IsConcatenation(binary.Parent))
                    Report(binary, report, Concat, "Concatenating strings");
                break;
            case ICompoundAssignmentOperation
            {
                OperatorKind: BinaryOperatorKind.Add,
                Type.SpecialType: SpecialType.System_String,
            }:
                Report(operation, report, Concat, "Appending to a string");
                break;
            case IInterpolatedStringOperation interpolation:
                Interpolation(interpolation, report);
                break;
            case IAnonymousFunctionOperation lambda:
                if (!lambda.Symbol.IsStatic && Captured(lambda.Body, lambda.Symbol) is { } captured)
                    Report(lambda, report, Capture, $"A lambda capturing '{captured}'");
                break;
            case IDelegateCreationOperation creation:
                MethodGroup(creation, report);
                break;
            case IConversionOperation conversion:
                if (conversion.GetConversion().IsBoxing && conversion.Operand.Type is { } from)
                    Report(
                        conversion,
                        report,
                        Boxing,
                        from.ToDisplayString(),
                        conversion.Type?.ToDisplayString() ?? "object"
                    );
                break;
            case IReturnOperation { Kind: OperationKind.YieldReturn }:
                Report(
                    operation,
                    report,
                    Iterator,
                    "This member is an iterator, which allocates its state machine"
                );
                break;
            case IForEachLoopOperation loop:
                ForEach(loop, report);
                break;
        }
    }

    static void Invocation(IInvocationOperation invocation, Action<Diagnostic> report)
    {
        var method = invocation.TargetMethod;
        if (method.ContainingNamespace?.ToDisplayString() == "System.Linq")
            Report(invocation, report, Linq, method.Name);
        else if (
            method.ContainingType.SpecialType == SpecialType.System_String
            && method.Name == "Concat"
        )
            Report(invocation, report, Concat, "string.Concat");
        else if (BoxedReceiver(invocation) is { } receiver)
            Report(
                invocation,
                report,
                Boxing,
                receiver.ToDisplayString(),
                method.ContainingType.ToDisplayString()
            );

        Arguments(invocation, invocation.Arguments, method.Name, report);
    }

    // A value calling what Enum, ValueType or object declares is boxed for it; of type parameters, surely only an enum.
    static ITypeSymbol? BoxedReceiver(IInvocationOperation invocation) =>
        invocation
            is {
                TargetMethod: { IsStatic: false, ContainingType.SpecialType: var declarer },
                Instance.Type: { IsValueType: true } receiver,
            }
        && declarer
            is SpecialType.System_Object
                or SpecialType.System_ValueType
                or SpecialType.System_Enum
        && (
            receiver is not ITypeParameterSymbol parameter
            || parameter.ConstraintTypes.Any(t => t.SpecialType == SpecialType.System_Enum)
        )
            ? receiver
            : null;

    static void Arguments(
        IOperation operation,
        ImmutableArray<IArgumentOperation> arguments,
        string method,
        Action<Diagnostic> report
    )
    {
        foreach (var argument in arguments)
        {
            if (
                argument is
                {
                    ArgumentKind: ArgumentKind.ParamArray,
                    Value: IArrayCreationOperation { Initializer.ElementValues.Length: > 0 },
                }
            )
                Report(operation, report, Params, method);
        }
    }

    static bool IsConcatenation(IOperation? operation) =>
        operation
            is IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.Add,
                Type.SpecialType: SpecialType.System_String,
                ConstantValue.HasValue: false,
            };

    // An interpolation into a handler other than a string's writes into the handler's own buffer, joined ones too.
    static void Interpolation(IInterpolatedStringOperation interpolation, Action<Diagnostic> report)
    {
        if (interpolation.ConstantValue.HasValue)
            return;

        var parent = interpolation.Parent;
        while (parent is IInterpolatedStringAdditionOperation)
            parent = parent.Parent;
        if (
            parent is not IInterpolatedStringHandlerCreationOperation { Type: { } handler }
            || handler.ToDisplayString() == StringHandler
        )
            Report(interpolation, report, Concat, "Interpolating a string");
    }

    // What string.Create interpolates into: it ends in a new string all the same.
    const string StringHandler = "System.Runtime.CompilerServices.DefaultInterpolatedStringHandler";

    static void MethodGroup(IDelegateCreationOperation creation, Action<Diagnostic> report)
    {
        if (creation.Target is not IMethodReferenceOperation { Method: { IsStatic: false } method })
            return;

        if (method.MethodKind != MethodKind.LocalFunction)
        {
            Report(creation, report, Capture, $"A delegate to the instance method '{method.Name}'");
            return;
        }

        if (
            BodyOf(method, creation.SemanticModel) is { } body
            && Captured(body, method) is { } captured
        )
            Report(
                creation,
                report,
                Capture,
                $"A delegate to '{method.Name}', which captures '{captured}'"
            );
    }

    static IOperation? BodyOf(IMethodSymbol localFunction, SemanticModel? model)
    {
        foreach (var reference in localFunction.DeclaringSyntaxReferences)
        {
            if (
                model?.GetOperation(reference.GetSyntax()) is ILocalFunctionOperation
                {
                    Body: { } body
                }
            )
                return body;
        }
        return null;
    }

    // A local function declared outside the body brings what it captures along when the body calls or names it.
    static string? Captured(
        IOperation body,
        IMethodSymbol function,
        HashSet<IMethodSymbol>? followed = null
    )
    {
        foreach (var operation in body.DescendantsAndSelf())
        {
            switch (operation)
            {
                case IInstanceReferenceOperation
                {
                    ReferenceKind: InstanceReferenceKind.ContainingTypeInstance,
                }:
                    return "this";
                case ILocalReferenceOperation local when !Within(local.Local, function):
                    return local.Local.Name;
                case IParameterReferenceOperation parameter
                    when !Within(parameter.Parameter, function):
                    return parameter.Parameter.Name;
                case IInvocationOperation { TargetMethod: var callee }
                    when CapturedThrough(callee, function, operation, ref followed) is { } captured:
                    return captured;
                case IMethodReferenceOperation { Method: var named }
                    when CapturedThrough(named, function, operation, ref followed) is { } captured:
                    return captured;
            }
        }
        return null;
    }

    static string? CapturedThrough(
        IMethodSymbol method,
        IMethodSymbol function,
        IOperation use,
        ref HashSet<IMethodSymbol>? followed
    )
    {
        if (
            method is not { MethodKind: MethodKind.LocalFunction, IsStatic: false }
            || Within(method, function)
            || !(followed ??= new(SymbolEqualityComparer.Default)).Add(method.OriginalDefinition)
            || BodyOf(method.OriginalDefinition, use.SemanticModel) is not { } body
        )
            return null;
        return Captured(body, method.OriginalDefinition, followed);
    }

    static bool Within(ISymbol symbol, IMethodSymbol function)
    {
        for (var s = symbol.ContainingSymbol; s is not null; s = s.ContainingSymbol)
        {
            if (SymbolEqualityComparer.Default.Equals(s, function))
                return true;
        }
        return false;
    }

    // The compiler walks an array or a string by index, whatever enumerator it reports.
    static void ForEach(IForEachLoopOperation loop, Action<Diagnostic> report)
    {
        if (
            loop is { Syntax: CommonForEachStatementSyntax syntax }
            && loop.SemanticModel is { } model
            && model.GetTypeInfo(syntax.Expression).Type
                is not (IArrayTypeSymbol or { SpecialType: SpecialType.System_String })
            && model.GetForEachStatementInfo(syntax).GetEnumeratorMethod
                is { ReturnType.IsReferenceType: true } enumerator
        )
            Report(
                loop,
                report,
                Iterator,
                $"Enumerating through '{enumerator.ContainingType.ToDisplayString()}' allocates its enumerator"
            );
    }

    static void Report(
        IOperation operation,
        Action<Diagnostic> report,
        DiagnosticDescriptor rule,
        params object[] arguments
    )
    {
        if (!InAttribute(operation))
            report(Diagnostic.Create(rule, operation.Syntax.GetLocation(), arguments));
    }

    // An attribute's arguments compile to metadata, so no frame runs them.
    static bool InAttribute(IOperation operation)
    {
        for (IOperation? o = operation; o is not null; o = o.Parent)
        {
            if (o is IAttributeOperation)
                return true;
        }
        return false;
    }
}
