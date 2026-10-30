using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Analyzers;

/// <summary>Holds every member a frame path calls in its own compilation to <see cref="FramePathAnalyzer"/>'s bans
/// (TRL2001–2006): a method, accessor, indexer, constructor, operator or local function that a frame-path member calls,
/// directly or through other members of the compilation, reports what it allocates under the same rule, naming the
/// frame-path member that reaches it. Only calls reach: a method group, an event handler or a lambda carries none of
/// what it would run, and neither a call into another assembly nor one that builds what a <c>throw</c> throws, which
/// runs only as the frame faults, is followed; nor is a member marked <c>[Tralys.Ui.ColdPath]</c>. A call through an
/// interface or a virtual member reaches only that member's own body, never an implementation or override.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FramePathReachAnalyzer : DiagnosticAnalyzer
{
    static readonly ImmutableDictionary<string, DiagnosticDescriptor> Reached =
        FramePathAnalyzer.Rules.ToImmutableDictionary(
            rule => rule.Id,
            rule =>
                Descriptors.CompilationEndError(
                    rule.Id,
                    rule.Title.ToString(CultureInfo.InvariantCulture),
                    "{0} (on the frame path: {1})"
                )
        );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [.. Reached.Values];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var graph = new CallGraph(start.Compilation);
            start.RegisterOperationBlockAction(graph.Add);
            start.RegisterCompilationEndAction(graph.Report);
        });
    }

    sealed class Member(ISymbol symbol, bool root)
    {
        public ISymbol Symbol => symbol;

        public bool Root { get; set; } = root;

        public List<ISymbol> Calls { get; } = [];

        public List<Diagnostic> Bans { get; } = [];
    }

    sealed class CallGraph(Compilation compilation)
    {
        static readonly SymbolDisplayFormat Format = new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            memberOptions: SymbolDisplayMemberOptions.IncludeContainingType
        );

        readonly IAssemblySymbol _assembly = compilation.Assembly;
        readonly IMethodSymbol? _dispose = compilation
            .GetSpecialType(SpecialType.System_IDisposable)
            .GetMembers("Dispose")
            .OfType<IMethodSymbol>()
            .FirstOrDefault();
        readonly ConcurrentBag<Member> _members = [];
        readonly ConcurrentBag<ISymbol> _initializers = [];
        readonly ConcurrentBag<ISymbol> _chained = [];

        public void Add(OperationBlockAnalysisContext context)
        {
            if (
                context.OwningSymbol is not (IMethodSymbol or IFieldSymbol or IPropertySymbol)
                || Normalize(context.OwningSymbol) is not { } owner
            )
                return;
            var member = new Member(owner, FramePaths.OnFramePath(context.OwningSymbol));
            _members.Add(member);
            var bans = !member.Root && !context.IsGeneratedCode;
            foreach (var block in context.OperationBlocks)
                Visit(block, member, bans, follows: true);

            switch (owner)
            {
                case IFieldSymbol { IsStatic: false } or IPropertySymbol { IsStatic: false }:
                    _initializers.Add(owner);
                    break;
                case IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
                    when ChainsToThis(constructor, context.OperationBlocks):
                    _chained.Add(owner);
                    break;
            }
        }

        // Null inside a lambda that is no carrier, whose body runs later if ever.
        void Visit(IOperation operation, Member? member, bool bans, bool follows)
        {
            switch (operation)
            {
                case IAttributeOperation or INameOfOperation:
                    return;
                case IAnonymousFunctionOperation lambda:
                    if (member is not null && bans)
                        FramePathAnalyzer.Check(lambda, member.Bans.Add);
                    if (FramePaths.IsCarrier(lambda))
                    {
                        member = new Member(lambda.Symbol, root: true);
                        _members.Add(member);
                        bans = false;
                        follows = true;
                    }
                    else
                        member = null;
                    break;
                case ILocalFunctionOperation local:
                    if (member is not null)
                    {
                        member = new Member(Normalize(local.Symbol)!, root: false);
                        _members.Add(member);
                        follows = true;
                    }
                    break;
                default:
                    if (member is null)
                        break;
                    if (bans)
                        FramePathAnalyzer.Check(operation, member.Bans.Add);
                    if (operation is IThrowOperation)
                        follows = false;
                    else if (follows)
                        Calls(operation, member.Calls);
                    break;
            }
            foreach (var child in operation.ChildOperations)
                Visit(child, member, bans, follows);
        }

        void Calls(IOperation operation, List<ISymbol> calls)
        {
            switch (operation)
            {
                case IInvocationOperation invocation:
                    Call(invocation.TargetMethod, calls);
                    break;
                case IObjectCreationOperation creation:
                    Call(creation.Constructor, calls);
                    break;
                case IPropertyReferenceOperation reference:
                    var (reads, writes) = Uses(reference);
                    if (reads)
                        Call(reference.Property.GetMethod, calls);
                    if (writes)
                        Call(reference.Property.SetMethod, calls);
                    break;
                case IEventAssignmentOperation
                {
                    EventReference: IEventReferenceOperation { Event: var e },
                } assignment:
                    Call(assignment.Adds ? e.AddMethod : e.RemoveMethod, calls);
                    break;
                case IBinaryOperation binary:
                    Call(binary.OperatorMethod, calls);
                    break;
                case IUnaryOperation unary:
                    Call(unary.OperatorMethod, calls);
                    break;
                case IIncrementOrDecrementOperation step:
                    Call(step.OperatorMethod, calls);
                    break;
                case ICompoundAssignmentOperation compound:
                    Call(compound.OperatorMethod, calls);
                    Call(compound.InConversion.MethodSymbol, calls);
                    Call(compound.OutConversion.MethodSymbol, calls);
                    break;
                case IConversionOperation conversion:
                    Call(conversion.OperatorMethod, calls);
                    break;
                case IForEachLoopOperation { Syntax: CommonForEachStatementSyntax syntax } loop
                    when loop.SemanticModel is { } model:
                    var info = model.GetForEachStatementInfo(syntax);
                    Call(info.GetEnumeratorMethod, calls);
                    Call(info.MoveNextMethod, calls);
                    Call(info.CurrentProperty?.GetMethod, calls);
                    Call(info.DisposeMethod, calls);
                    break;
                case IUsingOperation @using:
                    Disposed(@using.Resources, calls);
                    break;
                case IUsingDeclarationOperation declaration:
                    Disposed(declaration.DeclarationGroup, calls);
                    break;
            }
        }

        static (bool Reads, bool Writes) Uses(IPropertyReferenceOperation reference)
        {
            IOperation target = reference;
            var parent = reference.Parent;
            while (parent is ITupleOperation)
            {
                target = parent;
                parent = parent.Parent;
            }
            return parent switch
            {
                IDeconstructionAssignmentOperation deconstruction
                    when target != reference && deconstruction.Target == target => (false, true),
                ISimpleAssignmentOperation assignment when assignment.Target == reference =>
                    assignment.IsRef ? (true, false) : (false, true),
                ICompoundAssignmentOperation compound when compound.Target == reference => (
                    true,
                    true
                ),
                ICoalesceAssignmentOperation coalesce when coalesce.Target == reference => (
                    true,
                    true
                ),
                IIncrementOrDecrementOperation => (true, true),
                _ => (true, false),
            };
        }

        void Disposed(IOperation resources, List<ISymbol> calls)
        {
            if (resources is IVariableDeclarationGroupOperation group)
            {
                foreach (var declaration in group.Declarations)
                {
                    foreach (var declarator in declaration.Declarators)
                        Disposed(declarator.Symbol.Type, calls);
                }
            }
            else if (resources.Type is { } type)
                Disposed(type, calls);
        }

        void Disposed(ITypeSymbol type, List<ISymbol> calls)
        {
            if (
                _dispose is not null
                && type.FindImplementationForInterfaceMember(_dispose) is { } dispose
            )
                Call(dispose, calls);
            else if (type.IsRefLikeType)
            {
                foreach (var member in type.GetMembers("Dispose"))
                {
                    if (member is IMethodSymbol { IsStatic: false, Parameters.IsEmpty: true })
                        Call(member, calls);
                }
            }
        }

        void Call(ISymbol? callee, List<ISymbol> calls)
        {
            if (Normalize(callee) is { } symbol)
                calls.Add(symbol);
        }

        ISymbol? Normalize(ISymbol? symbol)
        {
            switch (symbol)
            {
                case IMethodSymbol method:
                    method = (method.ReducedFrom ?? method).OriginalDefinition;
                    symbol = method.PartialImplementationPart ?? method;
                    break;
                case IPropertySymbol property:
                    symbol =
                        property.OriginalDefinition.PartialImplementationPart
                        ?? property.OriginalDefinition;
                    break;
                case IFieldSymbol field:
                    symbol = field.OriginalDefinition;
                    break;
                default:
                    return null;
            }
            return SymbolEqualityComparer.Default.Equals(symbol.ContainingAssembly, _assembly)
                ? symbol
                : null;
        }

        static bool ChainsToThis(IMethodSymbol constructor, ImmutableArray<IOperation> blocks)
        {
            foreach (var block in blocks)
            {
                if (block is not IConstructorBodyOperation { Initializer: { } initializer })
                    continue;
                foreach (var operation in initializer.DescendantsAndSelf())
                {
                    if (
                        operation
                            is IInvocationOperation
                            {
                                TargetMethod: { MethodKind: MethodKind.Constructor } target,
                            }
                        && SymbolEqualityComparer.Default.Equals(
                            target.ContainingType.OriginalDefinition,
                            constructor.ContainingType.OriginalDefinition
                        )
                    )
                        return true;
                }
            }
            return false;
        }

        public void Report(CompilationAnalysisContext context)
        {
            var members = new Dictionary<ISymbol, Member>(SymbolEqualityComparer.Default);
            foreach (var member in _members)
            {
                if (!members.TryGetValue(member.Symbol, out var known))
                {
                    members.Add(member.Symbol, member);
                    continue;
                }
                known.Root |= member.Root;
                known.Calls.AddRange(member.Calls);
                known.Bans.AddRange(member.Bans);
            }

            var initializers = new Dictionary<ISymbol, List<ISymbol>>(
                SymbolEqualityComparer.Default
            );
            foreach (var initializer in _initializers)
            {
                if (!initializers.TryGetValue(initializer.ContainingType, out var list))
                    initializers.Add(initializer.ContainingType, list = []);
                list.Add(initializer);
            }
            var chained = new HashSet<ISymbol>(_chained, SymbolEqualityComparer.Default);

            var roots = new List<Member>();
            foreach (var member in members.Values)
            {
                if (member.Root)
                    roots.Add(member);
            }
            roots.Sort(static (a, b) => Order(a.Symbol, b.Symbol));

            var reachedFrom = new Dictionary<ISymbol, ISymbol?>(SymbolEqualityComparer.Default);
            var cold = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var queue = new Queue<ISymbol>();
            foreach (var root in roots)
            {
                reachedFrom.Add(root.Symbol, null);
                queue.Enqueue(root.Symbol);
            }
            while (queue.Count > 0)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                var caller = queue.Dequeue();
                if (members.TryGetValue(caller, out var member))
                {
                    foreach (var callee in member.Calls)
                        Reach(callee, caller);
                }
                if (
                    caller is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor
                    && !chained.Contains(constructor)
                )
                {
                    if (initializers.TryGetValue(constructor.ContainingType, out var fields))
                    {
                        foreach (var field in fields)
                            Reach(field, caller);
                    }
                    if (constructor.IsImplicitlyDeclared)
                    {
                        foreach (
                            var inherited in constructor
                                .ContainingType
                                .BaseType
                                ?.InstanceConstructors
                                ?? []
                        )
                        {
                            if (inherited.Parameters.IsEmpty && Normalize(inherited) is { } chain)
                                Reach(chain, caller);
                        }
                    }
                }
            }

            foreach (var reached in reachedFrom)
            {
                if (
                    reached.Value is null
                    || !members.TryGetValue(reached.Key, out var member)
                    || member.Bans.Count == 0
                )
                    continue;
                var path = Path(reached.Key, reachedFrom);
                foreach (var ban in member.Bans)
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            Reached[ban.Id],
                            ban.Location,
                            ban.GetMessage(CultureInfo.InvariantCulture),
                            path
                        )
                    );
            }

            void Reach(ISymbol callee, ISymbol caller)
            {
                if (reachedFrom.ContainsKey(callee) || cold.Contains(callee))
                    return;
                if (FramePaths.IsCold(callee))
                {
                    cold.Add(callee);
                    return;
                }
                reachedFrom.Add(callee, caller);
                queue.Enqueue(callee);
            }
        }

        static string Path(ISymbol reached, Dictionary<ISymbol, ISymbol?> reachedFrom)
        {
            var names = new List<string>();
            for (ISymbol? symbol = reached; symbol is not null; symbol = reachedFrom[symbol])
                names.Add(Name(symbol));
            names.Reverse();
            return string.Join(" → ", names);
        }

        static string Name(ISymbol symbol) =>
            symbol switch
            {
                IMethodSymbol { MethodKind: MethodKind.AnonymousFunction } lambda => "a lambda in "
                    + Name(lambda.ContainingSymbol),
                IMethodSymbol { MethodKind: MethodKind.LocalFunction } local => Name(
                    local.ContainingSymbol
                )
                    + "."
                    + local.Name,
                IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => "new "
                    + constructor.ContainingType.ToDisplayString(Format),
                _ => symbol.ToDisplayString(Format),
            };

        static int Order(ISymbol a, ISymbol b)
        {
            var x = a.Locations.FirstOrDefault();
            var y = b.Locations.FirstOrDefault();
            var byFile = string.CompareOrdinal(x?.SourceTree?.FilePath, y?.SourceTree?.FilePath);
            if (byFile != 0)
                return byFile;
            var bySpan = (x?.SourceSpan.Start ?? 0).CompareTo(y?.SourceSpan.Start ?? 0);
            return bySpan != 0
                ? bySpan
                : string.CompareOrdinal(a.ToDisplayString(), b.ToDisplayString());
        }
    }
}
