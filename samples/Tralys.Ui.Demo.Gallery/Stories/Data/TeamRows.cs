using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

/// <summary>One member of a team, as a table row.</summary>
sealed record TeamMember(string Name, string Role, int Level);

/// <summary>A team's heading among the rows of a table listing several teams.</summary>
sealed record TeamHeading(string Team, string Summary);

static class TeamRows
{
    public static readonly KeyOf<TeamMember> ByName = static m => m.Name;

    public static readonly TextOf<TeamMember> Name = static m => m.Name;

    public static readonly TextOf<TeamMember> Role = static m => m.Role;

    public static readonly Comparison<TeamMember> Levels = static (a, b) =>
        a.Level.CompareTo(b.Level);

    public static readonly Slot<TeamMember> NameText = Slot<TeamMember>.Of(
        static (ui, m, _) =>
            ui.Text(m.Name, fontWeight: ui.Theme.Weight(WeightToken.Interface), wrap: true)
    );

    public static readonly Slot<TeamMember> RoleText = Slot<TeamMember>.Of(
        static (ui, m, _) =>
            ui.Text(
                m.Role,
                color: Muted(ui),
                fontWeight: ui.Theme.Weight(WeightToken.Interface),
                wrap: true
            )
    );

    public static readonly Slot<TeamMember> LevelText = Slot<TeamMember>.Of(
        static (ui, m, _) =>
            ui.Text(
                ui.Format($"{m.Level}"),
                fontWeight: ui.Theme.Weight(WeightToken.Interface),
                wrap: true
            )
    );

    public static readonly Slot<TeamMember> LevelPlain = Slot<TeamMember>.Of(
        static (ui, m, _) => ui.Text(ui.Format($"{m.Level}"))
    );

    public static readonly TeamMember[] Five =
    [
        new("Alice", "Engineer", 120),
        new("Bram", "Designer", 98),
        new("Cyra", "Writer", 104),
        new("Doran", "Analyst", 87),
        new("Elin", "Manager", 112),
    ];

    /// <summary>The team by level, highest first, as a roster sorted by level shows it.</summary>
    public static readonly TeamMember[] Ranked = [Five[0], Five[4], Five[2], Five[1], Five[3]];

    public static readonly TeamMember[] TenThousand = Members(10_000);

    public static readonly TeamMember[] Staff = Members(42);

    public static TeamMember[] Members(int count)
    {
        string[] roles = ["Engineer", "Designer", "Writer", "Analyst", "Manager"];
        var members = new TeamMember[count];
        for (var i = 0; i < members.Length; i++)
            members[i] = new TeamMember(
                $"Member {i + 1:D5}",
                roles[i % roles.Length],
                i * 37 % 150 + 1
            );
        return members;
    }

    /// <summary>The muted text colour, which in a selected row gives way to the row's accent.</summary>
    [FramePath]
    public static Paint Muted(Ui ui) =>
        ui.IsSelected ? default(Paint) : ui.Theme.Color(ColorToken.FgMuted);

    public static TableSort ByLevel => new("Level", SortDirection.Descending);
}

/// <summary>A team's rows in the order a table's sort asks for, reordered in place as the sort changes, the way a view
/// model sorts the rows a table shows: names and roles ordinally, levels by value, descending as ascending reversed.</summary>
[FramePath]
sealed class TeamRoster(TeamMember[] members)
{
    static readonly Comparison<TeamMember> Names = static (a, b) =>
        string.CompareOrdinal(a.Name, b.Name);

    static readonly Comparison<TeamMember> Roles = static (a, b) =>
        string.CompareOrdinal(a.Role, b.Role);

    readonly TeamMember[] _sorted = new TeamMember[members.Length];
    TableSort _applied = new("", (SortDirection)byte.MaxValue);

    public RowList<TeamMember> Rows { get; } = new();

    public void Order(TableSort sort)
    {
        if (sort == _applied)
            return;
        _applied = sort;
        members.AsSpan().CopyTo(_sorted);
        if (sort.Direction != SortDirection.None)
        {
            _sorted
                .AsSpan()
                .Sort(
                    sort.Key == (UiKey)"Name" ? Names
                    : sort.Key == (UiKey)"Role" ? Roles
                    : TeamRows.Levels
                );
            if (sort.Direction == SortDirection.Descending)
                _sorted.AsSpan().Reverse();
        }
        foreach (var member in _sorted)
            Rows.Add(member);
        Rows.Apply();
    }
}
