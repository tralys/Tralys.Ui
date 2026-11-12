using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Fonts;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Table<TeamMember>>("Data")]
static class TableStories
{
    static readonly TableColumn<TeamMember>[] Sortable =
    [
        new()
        {
            Header = "Name",
            SortKey = "Name",
            Width = 160,
            Cell = TeamRows.NameText,
        },
        new()
        {
            Header = "Role",
            SortKey = "Role",
            Width = 120,
            Cell = TeamRows.RoleText,
        },
        new()
        {
            Header = "Level",
            SortKey = "Level",
            Width = 80,
            Cell = TeamRows.LevelText,
        },
    ];

    static readonly TableColumn<TeamMember>[] NameSorts =
    [
        new()
        {
            Header = "Name",
            SortKey = "Name",
            Width = 160,
            Text = TeamRows.Name,
        },
        new()
        {
            Header = "Role",
            Width = 120,
            Text = TeamRows.Role,
        },
    ];

    static readonly TableColumn<TeamMember>[] Many =
    [
        new()
        {
            Header = "Name",
            SortKey = "Name",
            Width = 160,
            Fill = true,
            Cell = Slot<TeamMember>.Of(
                static (ui, m, _) =>
                    ui.Text(m.Name, fontWeight: ui.Theme.Weight(WeightToken.Interface))
            ),
        },
        new()
        {
            Header = "Role",
            SortKey = "Role",
            Width = 120,
            Info = "What the member does",
            Cell = Slot<TeamMember>.Of(
                static (ui, m, _) =>
                    ui.Text(
                        m.Role,
                        color: TeamRows.Muted(ui),
                        fontWeight: ui.Theme.Weight(WeightToken.Interface)
                    )
            ),
        },
        new()
        {
            Header = "Level",
            SortKey = "Level",
            Width = 60,
            Cell = TeamRows.LevelText,
        },
    ];

    static readonly TableColumn<TeamMember>[] Templated =
    [
        new()
        {
            Header = "Name",
            Width = 160,
            Cell = TeamRows.NameText,
        },
        new()
        {
            Header = "Role",
            Width = 120,
            Cell = TeamRows.RoleText,
        },
    ];

    static readonly TableColumn<TeamMember>[] Unbound =
    [
        new() { Header = "Name", Width = 160 },
        new() { Header = "Role", Width = 120 },
    ];

    static readonly TableColumn<TeamMember>[] Shown =
    [
        new()
        {
            Header = "Name",
            Width = 160,
            Text = TeamRows.Name,
        },
        new()
        {
            Header = "Role",
            Width = 120,
            Text = TeamRows.Role,
        },
    ];

    static readonly TableColumn<TeamMember>[] NameAndLevel =
    [
        new()
        {
            Header = "Name",
            Width = 160,
            Text = TeamRows.Name,
        },
        new() { Header = "Level", Cell = TeamRows.LevelPlain },
    ];

    static readonly TableColumn<TeamMember>[] Filling =
    [
        new()
        {
            Header = "Name",
            Width = 120,
            Fill = true,
            Text = TeamRows.Name,
        },
        new()
        {
            Header = "Role",
            Width = 80,
            Fill = true,
            Text = TeamRows.Role,
        },
        new()
        {
            Header = "Level",
            Width = 60,
            Cell = TeamRows.LevelPlain,
        },
    ];

    static readonly IItemsSource<TeamMember> Five = ItemsSource.From(TeamRows.Ranked);

    static readonly IItemsSource<TeamMember> Nobody = ItemsSource.From<TeamMember>([]);

    [FramePath]
    public static void Default(Ui ui)
    {
        var sort = ui.State(TeamRows.ByLevel);
        var roster = ui.Remember(static () => new TeamRoster(TeamRows.Five));
        roster.Order(sort.Value);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            Caption(ui, "Click a header to sort; drag a divider to resize");
            ui.Table(
                realizeAll: true,
                items: roster.Rows,
                itemKey: TeamRows.ByName,
                columns: Sortable,
                sort: sort.Value,
                onSortChange: sort.Setter,
                resizable: true,
                width: 380
            );
        }
    }

    [FramePath]
    public static void UnsortableColumn(Ui ui)
    {
        var sort = ui.State(TeamRows.ByLevel);
        var roster = ui.Remember(static () => new TeamRoster(TeamRows.Five));
        roster.Order(sort.Value);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            Caption(ui, "Only Name sorts; Role has no hover");
            ui.Table(
                realizeAll: true,
                items: roster.Rows,
                itemKey: TeamRows.ByName,
                columns: NameSorts,
                sort: sort.Value,
                onSortChange: sort.Setter,
                width: 300
            );
        }
    }

    [FramePath]
    public static void ManyRows(Ui ui)
    {
        var sort = ui.State(TeamRows.ByLevel);
        var roster = ui.Remember(static () => new TeamRoster(TeamRows.TenThousand));
        roster.Order(sort.Value);
        var value = ui.State<UiKey>(roster.Rows[0].Name);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            Caption(
                ui,
                "Ten thousand rows: only those in view have rows, reused as it scrolls; headers sort and resize"
            );
            ui.Table(
                items: roster.Rows,
                itemKey: TeamRows.ByName,
                columns: Many,
                value: value.Value,
                onValueChange: value.Setter,
                selectable: true,
                sort: sort.Value,
                onSortChange: sort.Setter,
                resizable: true,
                width: 380,
                height: 320,
                alignSelf: AlignSelf.Start
            );
        }
    }

    [FramePath]
    public static void Plain(Ui ui) =>
        ui.Table(
            realizeAll: true,
            items: Five,
            itemKey: TeamRows.ByName,
            columns: Templated,
            width: 300
        );

    [FramePath]
    public static void SelectableColumns(Ui ui)
    {
        var value = ui.State<UiKey>(TeamRows.Ranked[1].Name);
        ui.Table(
            realizeAll: true,
            items: Five,
            itemKey: TeamRows.ByName,
            columns: Templated,
            value: value.Value,
            onValueChange: value.Setter,
            selectable: true,
            width: 300
        );
    }

    [FramePath]
    public static void Empty(Ui ui) =>
        ui.Table(
            items: Nobody,
            itemKey: TeamRows.ByName,
            columns: Unbound,
            emptyText: "Nobody has joined yet",
            width: 300,
            height: 120
        );

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(gap: 16))
        {
            ui.Table(
                realizeAll: true,
                items: Five,
                itemKey: TeamRows.ByName,
                columns: Shown,
                size: ComponentSize.Xs,
                width: 300
            );
            ui.Table(
                realizeAll: true,
                items: Five,
                itemKey: TeamRows.ByName,
                columns: Shown,
                size: ComponentSize.Md,
                width: 300
            );
        }
    }

    [FramePath]
    public static void DisplayMember(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            Caption(
                ui,
                "Cells shown by DisplayMemberBinding, compiled and typed by the rows; Level sizes to its widest cell"
            );
            ui.Table(
                realizeAll: true,
                items: Five,
                itemKey: TeamRows.ByName,
                columns: NameAndLevel,
                width: 300,
                alignSelf: AlignSelf.Start
            );
        }
    }

    [FramePath]
    public static void Fill(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            Caption(
                ui,
                "Name and Role fill what Level leaves of the table's width, each at least its own"
            );
            ui.Table(
                realizeAll: true,
                items: Five,
                itemKey: TeamRows.ByName,
                columns: Filling,
                width: 480,
                alignSelf: AlignSelf.Start
            );
        }
    }

    [FramePath]
    internal static void Caption(
        Ui ui,
        string text,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            color: ui.Theme.Color(ColorToken.FgMuted),
            fontWeight: ui.Theme.Weight(WeightToken.Interface),
            wrap: true,
            file: file,
            line: line
        );
}

[Stories<Table<object>>("Data")]
static class TableGroupStories
{
    static readonly object[] Rows =
    [
        new TeamHeading("Builders", "3 members"),
        new TeamMember("Alice", "Engineer", 120),
        new TeamMember("Bram", "Designer", 98),
        new TeamMember("Cyra", "Writer", 104),
        new TeamHeading("Planners", "2 members"),
        new TeamMember("Doran", "Analyst", 87),
        new TeamMember("Elin", "Manager", 112),
    ];

    static readonly IItemsSource<object> Items = ItemsSource.From(Rows);

    static readonly KeyOf<object> ByName = static row =>
        row switch
        {
            TeamHeading heading => heading.Team,
            TeamMember member => member.Name,
            _ => default,
        };

    static readonly TableColumn<object>[] Columns =
    [
        new()
        {
            Header = "Name",
            Width = 160,
            Text = static row => (row as TeamMember)?.Name,
        },
        new()
        {
            Header = "Role",
            Width = 120,
            Text = static row => (row as TeamMember)?.Role,
        },
    ];

    static readonly Predicate<object> IsHeading = static row => row is TeamHeading;

    static readonly Slot<object> Heading = Slot<object>.Of(
        static (ui, row, _) =>
        {
            var heading = (TeamHeading)row;
            var t = ui.Theme;
            using (ui.Box(align: Align.Center))
            {
                ui.Text(
                    heading.Team,
                    grow: 1,
                    color: t.Color(ColorToken.FgStrong),
                    fontWeight: FontWeight.SemiBold,
                    wrap: true
                );
                ui.Text(
                    heading.Summary,
                    color: t.Color(ColorToken.FgMuted),
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
            }
        }
    );

    [FramePath]
    public static void GroupHeaders(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            TableStories.Caption(
                ui,
                "A heading row spans the columns, shown through the template the list's selector picks for it"
            );
            ui.Table(
                realizeAll: true,
                items: Items,
                itemKey: ByName,
                columns: Columns,
                fullRow: Heading,
                isFullRow: IsHeading,
                width: 300,
                alignSelf: AlignSelf.Start
            );
        }
    }
}

[Stories<Table.HeaderRow<TeamMember>>("Data")]
static class TableHeaderRowStories
{
    static readonly TableColumn<TeamMember>[] Headed =
    [
        new()
        {
            Header = "Name",
            Width = 160,
            Text = TeamRows.Name,
        },
        new()
        {
            Header = "Role",
            Width = 120,
            Info = "What the member does",
            SortKey = "Role",
            Text = TeamRows.Role,
        },
        new()
        {
            Header = "Level",
            Width = 80,
            SortKey = "Level",
            Cell = TeamRows.LevelPlain,
        },
    ];

    static readonly TableColumn<TeamMember>[] Alone =
    [
        new() { Header = "Name", Width = 160 },
        new() { Header = "Role", Width = 100 },
        new() { Header = "Level" },
    ];

    [FramePath]
    public static void States(Ui ui)
    {
        var sort = ui.State(TeamRows.ByLevel);
        var roster = ui.Remember(static () => new TeamRoster(TeamRows.Five));
        roster.Order(sort.Value);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            TableStories.Caption(
                ui,
                "Level sorts descending, Role carries an info tip, and every column resizes by its gripper"
            );
            ui.Table(
                realizeAll: true,
                items: roster.Rows,
                itemKey: TeamRows.ByName,
                columns: Headed,
                sort: sort.Value,
                onSortChange: sort.Setter,
                resizable: true,
                width: 380,
                alignSelf: AlignSelf.Start
            );
        }
    }

    [FramePath]
    public static void Standalone(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 8, width: 360))
        {
            TableStories.Caption(
                ui,
                "A header row on its own, over a table's columns; Level sizes to its header"
            );
            ui.TableHeaderRow(columns: Alone);
        }
    }
}

[Stories<Table.Row<TeamMember>>("Data")]
static class TableRowStories
{
    static readonly TableColumn<TeamMember>[] Pooled =
    [
        new()
        {
            Header = "Name",
            Width = 160,
            Fill = true,
            Text = TeamRows.Name,
        },
        new()
        {
            Header = "Role",
            Width = 100,
            Text = TeamRows.Role,
        },
        new()
        {
            Header = "Level",
            Width = 60,
            Cell = TeamRows.LevelPlain,
        },
    ];

    static readonly TableColumn<TeamMember>[] Locking =
    [
        new()
        {
            Header = "Name",
            Width = 160,
            Text = TeamRows.Name,
        },
        new()
        {
            Header = "Level",
            Width = 120,
            Cell = TeamRows.LevelPlain,
        },
    ];

    static readonly IItemsSource<TeamMember> Five = ItemsSource.From(TeamRows.Ranked);

    static readonly Predicate<TeamMember> Junior = static m => m.Level < 100;

    [FramePath]
    public static void PooledTable(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, width: 360, height: 200))
        {
            ui.TableHeaderRow(columns: Pooled);
            using (ui.Box(direction: Direction.Column, grow: 1, overflow: Overflow.ScrollY))
                foreach (var member in TeamRows.Ranked)
                    ui.TableRow(item: member, columns: Pooled, key: member.Name);
        }
    }

    [FramePath]
    public static void States(Ui ui)
    {
        var value = ui.State<UiKey>(TeamRows.Ranked[0].Name);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            TableStories.Caption(
                ui,
                "Rows selectable, Alice selected, and the members under level 100 disabled"
            );
            ui.Table(
                realizeAll: true,
                items: Five,
                itemKey: TeamRows.ByName,
                columns: Locking,
                value: value.Value,
                onValueChange: value.Setter,
                selectable: true,
                isRowDisabled: Junior,
                width: 300,
                alignSelf: AlignSelf.Start
            );
        }
    }
}
