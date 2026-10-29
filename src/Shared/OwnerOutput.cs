using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Tralys.Ui.CodeGen;

interface IOwnedMember
{
    Owner Owner { get; }

    Problem? Problem { get; }
}

static class OwnerOutput
{
    public static IncrementalValuesProvider<EquatableArray<T>> ByOwner<T>(
        this IncrementalValueProvider<ImmutableArray<T>> members
    )
        where T : IOwnedMember, IEquatable<T> => members.SelectMany(static (all, _) => Group(all));

    public static IncrementalValuesProvider<EquatableArray<T>> ByOwner<T>(
        this IncrementalValuesProvider<T> members
    )
        where T : IOwnedMember, IEquatable<T> => members.Collect().ByOwner();

    static ImmutableArray<EquatableArray<T>> Group<T>(ImmutableArray<T> all)
        where T : IOwnedMember, IEquatable<T>
    {
        var batches = ImmutableArray.CreateBuilder<EquatableArray<T>>();
        foreach (var group in all.GroupBy(m => m.Owner.Fqn, StringComparer.Ordinal))
            batches.Add(new EquatableArray<T>([.. group]));
        return batches.ToImmutable();
    }

    public static void Emit<T>(
        SourceProductionContext context,
        ImmutableArray<T> members,
        DiagnosticDescriptor notPartial,
        string suffix,
        Func<Owner, List<T>, string> source
    )
        where T : IOwnedMember
    {
        if (members.Length == 0)
            return;

        var owner = members[0].Owner;
        if (!owner.IsPartial)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(notPartial, owner.Location?.ToLocation(), owner.NotPartial)
            );
            return;
        }

        var emitted = new List<T>();
        foreach (var member in members)
        {
            if (member.Problem is { } problem)
                problem.Report(context);
            else
                emitted.Add(member);
        }

        if (emitted.Count > 0)
            Output.Add(context, Output.HintName(owner.Key, suffix), source(owner, emitted));
    }
}
