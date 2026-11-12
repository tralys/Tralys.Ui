using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<DataTable<TeamMember>>("Data")]
static class DataTableStories
{
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

    static readonly TableColumn<TeamMember>[] Invoices =
    [
        new() { Header = "Client", Width = 160 },
        new() { Header = "Amount", Width = 120 },
    ];

    static readonly TableColumn<TeamMember>[] Searched =
    [
        new()
        {
            Header = "Name",
            SortKey = "Name",
            Width = 150,
            Text = TeamRows.Name,
        },
        new()
        {
            Header = "Role",
            SortKey = "Role",
            Width = 100,
            Text = TeamRows.Role,
        },
        new()
        {
            Header = "Level",
            SortKey = "Level",
            Width = 70,
            Cell = Slot<TeamMember>.Of(
                static (ui, m, _) =>
                    ui.Text(
                        ui.Format($"{m.Level}"),
                        fontWeight: ui.Theme.Weight(WeightToken.Interface)
                    )
            ),
            Compare = TeamRows.Levels,
        },
    ];

    static readonly IItemsSource<TeamMember> Nobody = ItemsSource.From<TeamMember>([]);

    static readonly IItemsSource<TeamMember> Staff = ItemsSource.From(TeamRows.Staff);

    [FramePath]
    public static void Default(Ui ui)
    {
        var roster = ui.Remember(static () => new TeamRoster(TeamRows.Five));
        roster.Order(TeamRows.ByLevel);
        ui.DataTable(
            items: roster.Rows,
            itemKey: TeamRows.ByName,
            columns: Shown,
            width: 320,
            height: 220
        );
    }

    [FramePath]
    public static void Empty(Ui ui) =>
        ui.DataTable(
            items: Nobody,
            itemKey: TeamRows.ByName,
            columns: Invoices,
            emptyText: "No invoices right now.",
            width: 320,
            height: 160
        );

    [FramePath]
    public static void Filtered(Ui ui)
    {
        var search = ui.Remember(static () => new ObservableText());
        var page = ui.State(1);
        var sort = ui.State<TableSort>(default);
        var roles = ui.Remember(static () => new RoleFilter());
        using (ui.Box(direction: Direction.Column, width: 360, height: 360))
        {
            using (ui.Box(margin: new Thickness(0, 0, 0, 6)))
            {
                ui.SearchInput(text: search, placeholder: "Search", grow: 1);
                ui.FilterButton(
                    content: roles.Boxes,
                    active: roles.Filtered(),
                    tooltip: "Filter roles",
                    margin: new Thickness(6, 0, 0, 0)
                );
            }
            ui.DataTable(
                items: Staff,
                itemKey: TeamRows.ByName,
                columns: Searched,
                page: page.Value,
                onPageChange: page.Setter,
                pageSize: 8,
                sort: sort.Value,
                onSortChange: sort.Setter,
                searchText: ui.Format($"{search.Span}"),
                filter: roles.Matches,
                noMatchText: "No members match your search.",
                grow: 1
            );
        }
    }

    [FramePath]
    public static void Paged(Ui ui)
    {
        var page = ui.State(1);
        var roster = ui.Remember(static () => new PagedRoster());
        roster.Load(page.Value);
        ui.DataTable(
            items: roster.Rows,
            itemKey: TeamRows.ByName,
            columns: Shown,
            page: page.Value,
            onPageChange: page.Setter,
            pageSize: PagedRoster.PageSize,
            totalItems: TeamRows.Staff.Length,
            width: 320,
            height: 220
        );
    }
}

/// <summary>A company's staff a page at a time, as a paged table's view model loads them.</summary>
[FramePath]
sealed class PagedRoster
{
    public const int PageSize = 8;

    int _loaded;

    public RowList<TeamMember> Rows { get; } = new();

    public void Load(int page)
    {
        if (page == _loaded)
            return;
        _loaded = page;
        var start = (page - 1) * PageSize;
        var end = Math.Min(start + PageSize, TeamRows.Staff.Length);
        for (var i = start; i < end; i++)
            Rows.Add(TeamRows.Staff[i]);
        Rows.Apply();
    }
}

/// <summary>Which roles a table of staff shows, as the filter popover's boxes tick them.</summary>
sealed class RoleFilter : ObservableObject
{
    static readonly string[] Roles = ["Engineer", "Designer", "Writer", "Analyst", "Manager"];

    readonly bool[] _shows = [true, true, true, true, true];
    readonly Callback<bool>[] _ticks = new Callback<bool>[Roles.Length];

    public RoleFilter()
    {
        Matches = Shows;
        for (var i = 0; i < Roles.Length; i++)
            _ticks[i] = Callback<bool>.Of(
                this,
                new Role(i),
                static (f, r, shows) => f.Show(r.Index, shows)
            );
        Boxes = Slot.Of(
            this,
            static (ui, f) =>
            {
                Observation.Read(f, 0);
                using (ui.Box(direction: Direction.Column, gap: 4))
                    for (var i = 0; i < Roles.Length; i++)
                        ui.Checkbox(
                            value: f._shows[i],
                            onValueChange: f._ticks[i],
                            label: Roles[i],
                            key: i
                        );
            }
        );
    }

    public Predicate<TeamMember> Matches { get; }

    public Slot Boxes { get; }

    [FramePath]
    bool Shows(TeamMember member)
    {
        Observation.Read(this, 0);
        return _shows[Array.IndexOf(Roles, member.Role)];
    }

    [FramePath]
    public bool Filtered()
    {
        Observation.Read(this, 0);
        foreach (var shows in _shows)
        {
            if (!shows)
                return true;
        }
        return false;
    }

    [FramePath]
    void Show(int role, bool shows)
    {
        _shows[role] = shows;
        NotifyObservers(0);
    }

    sealed record Role(int Index);
}
