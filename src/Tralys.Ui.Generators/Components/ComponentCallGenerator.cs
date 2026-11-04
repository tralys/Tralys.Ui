using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Generators.Components;

/// <summary>Emits one <c>ui.*</c> extension per component, in a static class in the component's namespace: its props in
/// declaration order, the <c>required</c> ones first and with no default, then the placement props, then the call site.
/// Every other argument defaults to zero, so an omitted one leaves the prop unset. It also registers the props type's
/// shape: the texts its host retains, its <c>Disabled</c> member, and the describer the inspector reads. A closed props type registers from a module initializer; props
/// generic in the component's type parameters from a static constructor written into the partial component, which the
/// core runs as it makes the component's instance, whichever way it mounts. A component the analyzers reject (TRL3001,
/// TRL3006) gets no call.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ComponentCallGenerator : IIncrementalGenerator
{
    const string Shape = "__PropsShape";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context
            .SyntaxProvider.CreateSyntaxProvider(
                static (node, _) =>
                    node is ClassDeclarationSyntax { BaseList: not null } declaration
                    && !declaration.Modifiers.Any(SyntaxKind.StaticKeyword)
                    && !declaration.Modifiers.Any(SyntaxKind.AbstractKeyword),
                static (ctx, ct) => Read(ctx, ct)
            )
            .Where(static model => model is not null)
            .Select(static (model, _) => model!.Value);

        context.RegisterSourceOutput(
            calls,
            static (spc, model) => Output.Add(spc, model.HintName, Source(model))
        );
    }

    static CallModel? Read(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (
            ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol type
            || type.DeclaringSyntaxReferences[0].GetSyntax(ct) != ctx.Node
        )
            return null;

        var compilation = ctx.SemanticModel.Compilation;
        if (
            compilation.GetTypeByMetadataName(UiShapes.Component) is not { } component
            || compilation.GetTypeByMetadataName(UiShapes.Placement) is not { } placement
            || !UiShapes.IsComponent(type, component)
            || UiShapes.PropsOf(type, component) is not { } props
            || !type.InstanceConstructors.Any(c =>
                c.Parameters.IsEmpty && c.DeclaredAccessibility == Accessibility.Public
            )
        )
            return null;

        var reach = Min(UiShapes.ReachOf(type), UiShapes.ReachOf(props));
        var reserved = UiShapes.ReservedNames(placement);
        var names = new HashSet<string>(StringComparer.Ordinal);
        var members = new List<Arg>();
        foreach (var member in UiShapes.Members(props))
        {
            var name = UiShapes.Camel(member.Name);
            if (reserved.Contains(name) || !names.Add(name))
                return null;
            reach = Min(reach, UiShapes.ReachOf(member.Type));
            members.Add(ArgOf(member));
        }
        if (reach == UiShapes.Reach.None)
            return null;

        var typeParameters = new List<ITypeParameterSymbol>();
        for (INamedTypeSymbol? t = type; t is not null; t = t.ContainingType)
            typeParameters.InsertRange(0, t.TypeParameters);

        var text = compilation.GetTypeByMetadataName(UiShapes.TextValue);
        var fresh = 0;
        var retain = new List<string>();
        foreach (var member in UiShapes.Members(props))
        {
            var name = Literals.Identifier(member.Name);
            if (Kept($"props.{name}", member.Type, text, 0, ref fresh) is { } kept)
                retain.Add($"{name} = {kept}");
        }

        var owner = Owner.From(type);
        var genericProps = UiShapes.HasTypeParameter(props);
        var declarations =
            genericProps
            && UiShapes.NotPartial(type) is null
            && UiShapes.OwnStaticConstructor(type) is null
                ? owner.Declarations.Split(';')
                : [];

        return new CallModel(
            Output.HintName(owner.Key, "Calls.g.cs"),
            type.ContainingNamespace.IsGlobalNamespace
                ? null
                : type.ContainingNamespace.ToDisplayString(),
            UiShapes.Chain(type, "") + "Calls",
            UiShapes.Chain(type, ""),
            reach == UiShapes.Reach.Public ? "public" : "internal",
            type.Fq(),
            props.Fq(),
            type.Fq().Replace('<', '{').Replace('>', '}'),
            new([.. typeParameters.Select(p => Literals.Identifier(p.Name))]),
            new([.. typeParameters.Select(Constraint).OfType<string>()]),
            type.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == UiShapes.Children),
            new([.. members]),
            new([.. UiShapes.Members(placement).Select(ArgOf)]),
            new([.. retain]),
            UiShapes
                .Members(props)
                .Any(m => m is { Name: "Disabled", Type.SpecialType: SpecialType.System_Boolean }),
            genericProps,
            Literals.Identifier(type.Name),
            new([.. declarations])
        );
    }

    static Arg ArgOf(IPropertySymbol member) =>
        new(
            Literals.Identifier(member.Name),
            Literals.Identifier(UiShapes.Camel(member.Name)),
            member.Type.FqNullable(),
            DefaultOf(member.Type),
            member.IsRequired
        );

    static string DefaultOf(ITypeSymbol type) =>
        type is ITypeParameterSymbol
            ? type.NullableAnnotation == NullableAnnotation.Annotated
                ? "default"
                : "default!"
            : type.NullableOf() is not null
                ? "null"
                : type.IsReferenceType
                    ? type.NullableAnnotation == NullableAnnotation.NotAnnotated
                        ? "default!"
                        : "null"
                    : type.SpecialType == SpecialType.System_Boolean
                        ? "false"
                        : type.SpecialType
                            is >= SpecialType.System_SByte
                                and <= SpecialType.System_Double
                            ? "0"
                            : "default";

    // A borrowed text is kept wherever a struct the props hold, at any depth, can be rebuilt with `with`.
    static string? Kept(
        string expression,
        ITypeSymbol type,
        INamedTypeSymbol? text,
        int depth,
        ref int fresh
    )
    {
        if (text is null || depth > 4)
            return null;
        if (SymbolEqualityComparer.Default.Equals(type, text))
            return $"text.Keep({expression})";
        if (type.NullableOf() is { } inner)
        {
            var value = $"__v{fresh++}";
            return Kept(value, inner, text, depth + 1, ref fresh) is { } kept
                ? $"{expression} is {{ }} {value} ? {kept} : null"
                : null;
        }
        if (
            type
            is not INamedTypeSymbol
            {
                TypeKind: TypeKind.Struct,
                SpecialType: SpecialType.None
            } nested
        )
            return null;

        var parts = new List<string>();
        foreach (var member in UiShapes.Members(nested))
        {
            var name = Literals.Identifier(member.Name);
            if (Kept($"{expression}.{name}", member.Type, text, depth + 1, ref fresh) is { } kept)
                parts.Add($"{name} = {kept}");
        }
        return parts.Count == 0 ? null : $"{expression} with {{ {string.Join(", ", parts)} }}";
    }

    static string? Constraint(ITypeParameterSymbol parameter)
    {
        var parts = new List<string>();
        if (parameter.HasReferenceTypeConstraint)
            parts.Add(
                parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    ? "class?"
                    : "class"
            );
        else if (parameter.HasUnmanagedTypeConstraint)
            parts.Add("unmanaged");
        else if (parameter.HasValueTypeConstraint)
            parts.Add("struct");
        else if (parameter.HasNotNullConstraint)
            parts.Add("notnull");
        parts.AddRange(parameter.ConstraintTypes.Select(t => t.FqNullable()));
        if (parameter.HasConstructorConstraint)
            parts.Add("new()");
        if (parameter.AllowsRefLikeType)
            parts.Add("allows ref struct");
        return parts.Count == 0
            ? null
            : $"where {Literals.Identifier(parameter.Name)} : {string.Join(", ", parts)}";
    }

    static UiShapes.Reach Min(UiShapes.Reach a, UiShapes.Reach b) => a < b ? a : b;

    static string Source(CallModel m)
    {
        var w = new CodeWriter();
        Output.Header(w);
        w.Line("#pragma warning disable CA2255, CS0612, CS0618");
        w.Line();
        Output.Namespace(w, m.Namespace);

        var typeParameters = m.TypeParameters.Items.IsEmpty
            ? ""
            : "<" + string.Join(", ", m.TypeParameters.Items) + ">";
        var shape = m.GenericProps ? Shape + typeParameters : Shape;

        w.Line(
            "[global::System.CodeDom.Compiler.GeneratedCode(\"Tralys.Ui.Generators\", \"1.0\")]"
        );
        using (w.Block($"{m.Access} static class {Literals.Identifier(m.ClassName)}"))
        {
            w.Line($"/// <inheritdoc cref=\"{m.Cref}\"/>");
            w.Line("[global::Tralys.Ui.FramePath]");
            using (w.Block(Signature(m, typeParameters)))
            {
                var lowering = m.Children ? "return ui.ComponentWithChildren" : "ui.Component";
                w.Line($"{lowering}<{m.Component}, {m.Props}>(");
                using (w.Indented())
                {
                    Initializer(w, m.Props, m.Members.Items);
                    Initializer(w, "global::Tralys.Ui.Placement", m.Placement.Items);
                    w.Line("key,");
                    w.Line("file,");
                    w.Line("line");
                }
                w.Line(");");
            }

            if (!m.GenericProps)
            {
                w.Line();
                w.Line(
                    "[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]"
                );
                w.Line("[global::System.Runtime.CompilerServices.ModuleInitializer]");
                w.Line(
                    $"internal static void RegisterProps() => global::Tralys.Ui.PropsShape<{m.Props}>.Register(new {Shape}());"
                );
            }
        }

        if (!m.Declarations.Items.IsEmpty)
        {
            w.Line();
            var scopes = new List<CodeWriter.Scope>();
            foreach (var level in m.Declarations.Items)
                scopes.Add(w.Block($"partial {level}"));
            w.Line(
                $"[global::System.CodeDom.Compiler.GeneratedCode(\"{UiShapes.GeneratedBy}\", \"1.0\")]"
            );
            w.Line(
                $"static {m.TypeName}() => global::Tralys.Ui.PropsShape<{m.Props}>.Register({shape}.Instance);"
            );
            Owner.Close(scopes);
        }

        w.Line();
        var header = new StringBuilder(
            $"file sealed class {shape} : global::Tralys.Ui.PropsShape<{m.Props}>"
        );
        if (m.GenericProps)
        {
            foreach (var constraint in m.Constraints.Items)
                header.Append("\n    ").Append(constraint);
        }
        using (w.Block(header.ToString()))
        {
            if (m.GenericProps)
            {
                w.Line($"public static readonly {shape} Instance = new();");
                w.Line();
            }
            if (m.Disabled)
            {
                w.Line($"public override bool? Disabled(in {m.Props} props) => props.Disabled;");
                w.Line();
            }
            if (!m.Retain.Items.IsEmpty)
            {
                w.Line(
                    $"public override void Retain(ref {m.Props} props, global::Tralys.Ui.TextKeeper text) =>"
                );
                using (w.Indented())
                    w.Line($"props = props with {{ {string.Join(", ", m.Retain.Items)} }};");
                w.Line();
            }
            using (
                w.Block(
                    $"public override void Describe(in {m.Props} props, global::Tralys.Ui.IPropsVisitor visitor)"
                )
            )
            {
                foreach (var member in m.Members.Items)
                    w.Line(
                        $"visitor.Prop({Literals.Quote(member.Member.TrimStart('@'))}, props.{member.Member});"
                    );
            }
        }
        return w.ToString();
    }

    static string Signature(CallModel m, string typeParameters)
    {
        var s = new StringBuilder();
        var returns = m.Children ? "global::Tralys.Ui.Scope" : "void";
        s.Append(
            $"{m.Access} static {returns} {Literals.Identifier(m.MethodName)}{typeParameters}(\n"
        );
        s.Append("    this global::Tralys.Ui.Ui ui,\n");
        foreach (var arg in m.Members.Items.Where(a => a.Required))
            s.Append($"    {arg.Type} {arg.Parameter},\n");
        foreach (var arg in m.Members.Items.Concat(m.Placement.Items).Where(a => !a.Required))
            s.Append($"    {arg.Type} {arg.Parameter} = {arg.Default},\n");
        s.Append("    global::Tralys.Ui.UiKey key = default,\n");
        s.Append(
            "    [global::System.Runtime.CompilerServices.CallerFilePath] string file = \"\",\n"
        );
        s.Append("    [global::System.Runtime.CompilerServices.CallerLineNumber] int line = 0\n");
        s.Append(')');
        foreach (var constraint in m.Constraints.Items)
            s.Append("\n    ").Append(constraint);
        return s.ToString();
    }

    static void Initializer(CodeWriter w, string type, IReadOnlyList<Arg> args)
    {
        if (args.Count == 0)
        {
            w.Line("default,");
            return;
        }
        w.Line($"new {type}");
        w.Line("{");
        using (w.Indented())
        {
            foreach (var arg in args)
                w.Line($"{arg.Member} = {arg.Parameter},");
        }
        w.Line("},");
    }

    readonly record struct Arg(
        string Member,
        string Parameter,
        string Type,
        string Default,
        bool Required
    );

    readonly record struct CallModel(
        string HintName,
        string? Namespace,
        string ClassName,
        string MethodName,
        string Access,
        string Component,
        string Props,
        string Cref,
        EquatableArray<string> TypeParameters,
        EquatableArray<string> Constraints,
        bool Children,
        EquatableArray<Arg> Members,
        EquatableArray<Arg> Placement,
        EquatableArray<string> Retain,
        bool Disabled,
        bool GenericProps,
        string TypeName,
        EquatableArray<string> Declarations
    );
}
