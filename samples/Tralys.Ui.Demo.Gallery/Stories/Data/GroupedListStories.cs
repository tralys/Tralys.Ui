using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<GroupedList<Team, string>>("Data")]
static class GroupedListStories
{
    static readonly Slot<Team> StrongHeader = Slot<Team>.Of(
        static (ui, team, _) => ui.Text(team.Name, color: ui.Theme.Color(ColorToken.FgStrong))
    );

    [FramePath]
    public static void Default(Ui ui)
    {
        var value = ui.State<UiKey>(default);
        ui.GroupedList(
            realizeAll: true,
            groups: Team.Few,
            groupKey: Team.ByName,
            groupItems: Team.Members,
            groupText: Team.Named,
            itemKey: Team.Member,
            itemText: Team.MemberText,
            value: value.Value,
            onValueChange: value.Setter,
            width: 240
        );
    }

    [FramePath]
    public static void ManyGroups(Ui ui)
    {
        var value = ui.State<UiKey>(default);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            HoldingListBoxStories.Caption(
                ui,
                "Twenty groups realized at once, as a short catalog is: a header toggles its group, and only items select"
            );
            ui.GroupedList(
                groups: Team.Many,
                groupKey: Team.ByName,
                groupItems: Team.Members,
                header: StrongHeader,
                itemKey: Team.Member,
                itemText: Team.MemberText,
                value: value.Value,
                onValueChange: value.Setter,
                realizeAll: true,
                width: 260,
                height: 300
            );
        }
    }
}

/// <summary>A group of members, keyed and headed by its name.</summary>
sealed class Team(string name, string[] members)
{
    public string Name { get; } = name;

    public IItemsSource<string> People { get; } = ItemsSource.From(members);

    public static readonly IItemsSource<Team> Few = ItemsSource.From(
        new Team[]
        {
            new("Engineers", ["Alice", "Dave"]),
            new("Designers", ["Bob", "Cyra"]),
            new("Analysts", ["Doran"]),
        }
    );

    public static readonly IItemsSource<Team> Many = ItemsSource.From(Teams(20));

    public static readonly KeyOf<Team> ByName = static team => team.Name;

    public static readonly TextOf<Team> Named = static team => team.Name;

    public static readonly Func<Team, IItemsSource<string>?> Members = static team => team.People;

    public static readonly KeyOf<string> Member = static name => name;

    public static readonly TextOf<string> MemberText = static name => name;

    static Team[] Teams(int count)
    {
        var teams = new Team[count];
        for (var g = 0; g < count; g++)
        {
            var members = new string[10];
            for (var i = 0; i < members.Length; i++)
                members[i] = $"Member {g + 1:D2}.{i + 1:D2}";
            teams[g] = new Team($"Group {g + 1:D2}", members);
        }
        return teams;
    }
}
