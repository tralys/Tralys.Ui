using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Mvvm.Generators;

/// <summary>Emits a <c>{Method}Command</c> property for every <c>[DelegateCommand]</c> method.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class DelegateCommandGenerator : IIncrementalGenerator
{
    static readonly DiagnosticDescriptor NotPartial = Descriptors.Error(
        "TRL3101",
        "Command owner must be partial",
        "'{0}' must be declared partial for its commands to be generated"
    );

    static readonly DiagnosticDescriptor BadReturnType = Descriptors.Error(
        "TRL3102",
        "Command method must return void or ValueTask",
        "'{0}' returns '{1}'; a [DelegateCommand] method returns void or ValueTask"
    );

    static readonly DiagnosticDescriptor ExtraParameters = Descriptors.Error(
        "TRL3103",
        "Command method takes more than one parameter",
        "'{0}' takes {1} parameters; a command passes only one"
    );

    static readonly DiagnosticDescriptor NotInstance = Descriptors.Error(
        "TRL3104",
        "Command method must be an instance method",
        "'{0}' is static; a [DelegateCommand] method is an instance method, since its command is a property of the instance"
    );

    static readonly DiagnosticDescriptor Overloaded = Descriptors.Error(
        "TRL3105",
        "Command method is overloaded",
        "'{0}' has another [DelegateCommand] overload, and {0}Command runs only one"
    );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var commands = context
            .SyntaxProvider.ForAttributeWithMetadataName(
                GeneratedMembers.DelegateCommandAttribute,
                static (node, _) => node is MethodDeclarationSyntax,
                static (ctx, _) => Read((IMethodSymbol)ctx.TargetSymbol)
            )
            .ByOwner();

        context.RegisterSourceOutput(commands, static (spc, owned) => Emit(spc, owned.Items));
    }

    static Command Read(IMethodSymbol method)
    {
        var location = LocationInfo.From(method.Locations.FirstOrDefault());
        var command = GeneratedMembers.CommandOf(method);

        Problem? problem = null;
        if (!method.ReturnsVoid && !GeneratedMembers.ReturnsValueTask(method))
            problem = new Problem(
                BadReturnType,
                location,
                method.Name,
                method.ReturnType.ToDisplayString()
            );
        else if (method.IsStatic)
            problem = new Problem(NotInstance, location, method.Name);
        else if (command is null)
            problem = new Problem(
                ExtraParameters,
                location,
                method.Name,
                method.Parameters.Length.ToString(CultureInfo.InvariantCulture)
            );

        return new Command(
            Owner.From(method.ContainingType),
            method.Name,
            command is { } shape
                ? $"{SyntaxFacts.GetText(shape.Accessibility)} {shape.Fq} {shape.Property}"
                : "",
            problem,
            location
        );
    }

    static void Emit(SourceProductionContext context, ImmutableArray<Command> owned)
    {
        var commands = ImmutableArray.CreateBuilder<Command>(owned.Length);
        foreach (var overloads in owned.GroupBy(c => c.Method))
        {
            commands.Add(overloads.First());
            foreach (var overload in overloads.Skip(1))
                commands.Add(
                    overload with
                    {
                        Problem = new Problem(Overloaded, overload.Location, overload.Method),
                    }
                );
        }

        OwnerOutput.Emit(context, commands.MoveToImmutable(), NotPartial, "Commands.g.cs", Source);
    }

    static string Source(Owner owner, List<Command> commands)
    {
        var w = new CodeWriter();
        Output.Header(w);
        w.Line("#pragma warning disable CS9258");
        var scopes = owner.Open(w);
        for (var i = 0; i < commands.Count; i++)
        {
            if (i > 0)
                w.Line();

            var command = commands[i];
            w.Line(
                $"{command.Declaration} => field ??= new({Literals.Identifier(command.Method)});"
            );
        }
        Owner.Close(scopes);
        return w.ToString();
    }

    readonly record struct Command(
        Owner Owner,
        string Method,
        string Declaration,
        Problem? Problem,
        LocationInfo? Location
    ) : IOwnedMember;
}
