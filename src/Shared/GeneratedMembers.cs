using Microsoft.CodeAnalysis;

namespace Tralys.Ui.CodeGen;

static class GeneratedMembers
{
    public const string DelegateCommandAttribute = "Tralys.Ui.Mvvm.DelegateCommandAttribute";
    public const string ObservablePropertyAttribute = "Tralys.Ui.Mvvm.ObservablePropertyAttribute";
    public const string NotifyPropertyChangedForAttribute =
        "Tralys.Ui.Mvvm.NotifyPropertyChangedForAttribute";
    public const string ObservableObject = "Tralys.Ui.Mvvm.ObservableObject";

    const string CommandType = "Tralys.Ui.Mvvm.Command";

    public static string Setter(string name) => "Set" + name;

    public static string Hook(string name) => "On" + name + "Changed";

    public static string Coerce(string name) => "On" + name + "Changing";

    public static string CommandProperty(string method) => method + "Command";

    public static bool IsNotifyPropertyChangedFor(AttributeData attribute) =>
        Is(attribute.AttributeClass, NotifyPropertyChangedForAttribute);

    public static Command? CommandOf(IMethodSymbol method)
    {
        if (!(method.ReturnsVoid || ReturnsValueTask(method)) || method.Parameters.Length > 1)
            return null;

        var parameter = method.Parameters.Length == 1 ? method.Parameters[0].Type : null;
        return new Command(
            CommandProperty(method.Name),
            CommandType,
            parameter,
            parameter is null ? Accessibility.Public : Effective(parameter)
        );
    }

    public static bool ReturnsValueTask(IMethodSymbol method) =>
        method.ReturnType is INamedTypeSymbol { Name: "ValueTask", IsGenericType: false } returned
        && returned.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks";

    static bool Is(INamedTypeSymbol? type, string metadataName) =>
        type is not null && type.IsDefinition(metadataName);

    // A command can be no more accessible than the parameter type it is closed over.
    static Accessibility Effective(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                return Effective(array.ElementType);
            case INamedTypeSymbol { TypeKind: not TypeKind.Error } named:
                var narrowest = Accessibility.Public;
                for (INamedTypeSymbol? t = named; t is not null; t = t.ContainingType)
                    narrowest = Narrower(narrowest, t.DeclaredAccessibility);
                foreach (var argument in named.TypeArguments)
                    narrowest = Narrower(narrowest, Effective(argument));
                return narrowest;
            default:
                return Accessibility.Public;
        }
    }

    static Accessibility Narrower(Accessibility a, Accessibility b) =>
        (a, b) switch
        {
            _ when a == b => a,
            (Accessibility.Private, _) or (_, Accessibility.Private) => Accessibility.Private,
            (Accessibility.Public, _) => b,
            (_, Accessibility.Public) => a,
            (Accessibility.ProtectedOrInternal, _) => b,
            (_, Accessibility.ProtectedOrInternal) => a,
            _ => Accessibility.ProtectedAndInternal,
        };

    public readonly struct Command(
        string property,
        string type,
        ITypeSymbol? parameter,
        Accessibility accessibility
    )
    {
        public string Property { get; } = property;

        public string Type { get; } = type;

        public ITypeSymbol? Parameter { get; } = parameter;

        public Accessibility Accessibility { get; } = accessibility;

        public string MetadataName => Parameter is null ? Type : Type + "`1";

        public string Fq =>
            Parameter is null ? "global::" + Type : $"global::{Type}<{Parameter.FqNullable()}>";
    }
}
