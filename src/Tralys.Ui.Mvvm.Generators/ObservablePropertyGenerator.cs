using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Mvvm.Generators;

/// <summary>Emits the body of every <c>[ObservableProperty]</c> partial property: a backing field, the change hook
/// and the cached args it raises.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class ObservablePropertyGenerator : IIncrementalGenerator
{
    const string Args = "global::System.ComponentModel.PropertyChangedEventArgs";
    const string ObservableObject = "global::" + GeneratedMembers.ObservableObject;

    static readonly DiagnosticDescriptor OwnerNotPartial = Descriptors.Error(
        "TRL3201",
        "Observable property owner must be partial",
        "'{0}' must be declared partial for its observable properties to be generated"
    );

    static readonly DiagnosticDescriptor NotObservableObject = Descriptors.Error(
        "TRL3202",
        "Observable property owner must be an ObservableObject",
        "'{0}' does not derive from Tralys.Ui.Mvvm.ObservableObject, which raises the changes of '{1}'"
    );

    static readonly DiagnosticDescriptor NotPartialInstance = Descriptors.Error(
        "TRL3203",
        "Observable property must be a partial instance property with no body",
        "'{0}' must be a partial instance property with no body of its own for its body to be generated"
    );

    static readonly DiagnosticDescriptor NotEquatable = Descriptors.Error(
        "TRL3204",
        "Struct observable property type must be equatable",
        "'{0}' is a '{1}', which must implement IEquatable<{1}>; comparing it otherwise boxes on every set"
    );

    static readonly DiagnosticDescriptor UnknownDependent = Descriptors.Error(
        "TRL3205",
        "Notified property does not exist",
        "'{0}' names '{1}' in [NotifyPropertyChangedFor], but its type has no property '{1}'"
    );

    static readonly DiagnosticDescriptor SetterCallbackTaken = new(
        "TRL3206",
        "Setter callback name is taken",
        "'{0}' gets no '{1}' setter callback, since its type already has a member named '{1}'",
        "Tralys.Ui",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    const string Callback = "Tralys.Ui.Callback`1";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var properties = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                GeneratedMembers.ObservablePropertyAttribute,
                static (node, _) => node is PropertyDeclarationSyntax,
                static (ctx, _) =>
                    Read(
                        (IPropertySymbol)ctx.TargetSymbol,
                        (PropertyDeclarationSyntax)ctx.TargetNode,
                        ctx.SemanticModel.Compilation.GetTypeByMetadataName(Callback) is not null
                    )
            )
            .ByOwner();

        context.RegisterSourceOutput(
            properties,
            static (spc, owned) =>
            {
                foreach (var model in owned.Items)
                    model.Warning?.Report(spc);
                OwnerOutput.Emit(
                    spc,
                    owned.Items,
                    OwnerNotPartial,
                    "ObservableProperties.g.cs",
                    Source
                );
            }
        );
    }

    static Model Read(
        IPropertySymbol property,
        PropertyDeclarationSyntax declaration,
        bool hasCallback
    )
    {
        var owner = property.ContainingType;
        var location = LocationInfo.From(property.Locations.FirstOrDefault());
        var dependents = property
            .GetAttributes()
            .Where(GeneratedMembers.IsNotifyPropertyChangedFor)
            .Select(a =>
                a.ConstructorArguments.Length == 1 && a.ConstructorArguments[0].Value is string name
                    ? name
                    : ""
            )
            .ToImmutableArray();

        Problem? problem = null;
        if (
            !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            || property.IsStatic
            || property.PartialImplementationPart is not null
        )
            problem = new Problem(NotPartialInstance, location, property.Name);
        else if (!owner.DerivesFrom(ObservableObject))
            problem = new Problem(
                NotObservableObject,
                location,
                owner.ToDisplayString(),
                property.Name
            );
        else if (!PropertyShape.IsEquatable(property.Type))
            problem = new Problem(
                NotEquatable,
                location,
                property.Name,
                property.Type.ToDisplayString()
            );
        else if (dependents.FirstOrDefault(name => !HasProperty(owner, name)) is { } unknown)
            problem = new Problem(UnknownDependent, location, property.Name, unknown);

        var setter = property.SetMethod is { IsInitOnly: false } method ? method : null;
        var writable = setter is not null;
        var callbackName = GeneratedMembers.Setter(property.Name);
        Problem? warning = null;
        string? callback = null;
        if (problem is null && setter is not null && hasCallback)
        {
            if (HasMember(owner, callbackName))
                warning = new Problem(SetterCallbackTaken, location, property.Name, callbackName);
            else
                callback = SyntaxFacts.GetText(setter.DeclaredAccessibility);
        }

        return new Model(
            Owner.From(owner),
            property.Name,
            property.Type.FqNullable(),
            PropertyShape.Hook(owner, property.Name, property.Type),
            PropertyShape.Coerce(owner, property.Name, property.Type),
            new EquatableArray<string>(dependents),
            problem,
            Accessors.From(property, declaration),
            IndexOf(owner, property),
            writable,
            callback,
            warning
        );
    }

    // Indices run on from the base types' properties, so a derived type's never share a base's bit.
    static int IndexOf(INamedTypeSymbol owner, IPropertySymbol property)
    {
        var index = 0;
        for (var t = owner.BaseType; t is not null; t = t.BaseType)
            index += t.GetMembers().Count(IsObservable);

        foreach (var member in owner.GetMembers())
        {
            if (SymbolEqualityComparer.Default.Equals(member, property))
                return index;
            if (IsObservable(member))
                index++;
        }
        return index;
    }

    static bool IsObservable(ISymbol member) =>
        member is IPropertySymbol { IsStatic: false } property
        && property
            .GetAttributes()
            .Any(a =>
                a.AttributeClass?.ToDisplayString() == GeneratedMembers.ObservablePropertyAttribute
            );

    static bool HasMember(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.BaseType)
        {
            if (!t.GetMembers(name).IsEmpty)
                return true;
        }
        return false;
    }

    static bool HasProperty(INamedTypeSymbol type, string name)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.BaseType)
        {
            if (t.GetMembers(name).Any(m => m is IPropertySymbol))
                return true;
        }
        return false;
    }

    static string Source(Owner owner, List<Model> models)
    {
        var w = new CodeWriter();
        Output.Header(w);
        w.Line("#pragma warning disable CS9258");
        var scopes = owner.Open(w);

        var raised = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var model in models)
        {
            raised.Add(model.Name);
            foreach (var dependent in model.Dependents.Items)
                raised.Add(dependent);
        }
        foreach (var name in raised)
            w.Line(
                $"static readonly {Args} {ArgsField(name)} = global::Tralys.Ui.Mvvm.PropertyChangedArgs.For({Literals.Quote(name)});"
            );

        foreach (var model in models)
        {
            w.Line();
            WriteProperty(w, owner, model);
        }
        Owner.Close(scopes);
        return w.ToString();
    }

    static void WriteProperty(CodeWriter w, Owner owner, Model m)
    {
        var accessors = m.Accessors;
        var writer = WriterField(m.Name);
        using (w.Block($"{accessors.Modifiers} {m.Type} {Literals.Identifier(m.Name)}"))
        {
            if (accessors.GetterModifier is { } getter)
            {
                using (w.Block($"{getter}get"))
                {
                    w.Line($"global::Tralys.Ui.Mvvm.Observation.Read(this, {m.Index});");
                    w.Line("return field;");
                }
            }
            if (accessors.SetterKeyword is { } setter)
            {
                using (w.Block($"{accessors.SetterModifier}{setter}"))
                {
                    if (m.Writable)
                    {
                        w.Line(
                            $"if (global::Tralys.Ui.Mvvm.Observation.Defers(this, value, {writer}))"
                        );
                        using (w.Indented())
                            w.Line("return;");
                    }
                    if (m.Coerce.Implemented)
                        w.Line($"{GeneratedMembers.Coerce(m.Name)}(field, ref value);");
                    w.Line(
                        $"if (global::System.Collections.Generic.EqualityComparer<{m.Type}>.Default.Equals(field, value))"
                    );
                    using (w.Indented())
                        w.Line("return;");
                    w.Line();
                    if (m.Hook.Implemented)
                    {
                        w.Line("var oldValue = field;");
                        w.Line("field = value;");
                        w.Line($"{GeneratedMembers.Hook(m.Name)}(oldValue, value);");
                    }
                    else
                        w.Line("field = value;");
                    w.Line($"NotifyObservers({m.Index});");
                    w.Line($"OnPropertyChanged({ArgsField(m.Name)});");
                    foreach (var dependent in m.Dependents.Items)
                        w.Line($"OnPropertyChanged({ArgsField(dependent)});");
                }
            }
        }

        if (m.Writable)
        {
            w.Line();
            w.Line(
                $"static readonly global::System.Action<{owner.Fqn}, {m.Type}> {writer} = static (owner, value) => owner.{Literals.Identifier(m.Name)} = value;"
            );
        }

        if (m.Callback is { } accessibility)
        {
            w.Line();
            w.Line(
                $"/// <summary>Sets <see cref=\"{Literals.Identifier(m.Name)}\"/> to the value it is invoked with.</summary>"
            );
            w.Line(
                $"{accessibility} global::Tralys.Ui.Callback<{m.Type}> {GeneratedMembers.Setter(m.Name)} => global::Tralys.Ui.Callback<{m.Type}>.Of(this, {writer});"
            );
        }

        if (m.Hook.Declaration is { } declaration)
        {
            w.Line();
            w.Line(declaration);
        }

        if (m.Coerce.Declaration is { } coercion)
        {
            w.Line();
            w.Line(coercion);
        }
    }

    static string ArgsField(string name) => "__" + name + "Changed";

    static string WriterField(string name) => "__Write" + name;

    readonly record struct Model(
        Owner Owner,
        string Name,
        string Type,
        HookShape Hook,
        HookShape Coerce,
        EquatableArray<string> Dependents,
        Problem? Problem,
        Accessors Accessors,
        int Index,
        bool Writable,
        string? Callback,
        Problem? Warning
    ) : IOwnedMember;
}
