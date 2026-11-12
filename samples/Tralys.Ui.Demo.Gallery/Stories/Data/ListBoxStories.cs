using Tralys.Ui.Components;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ListBox<string>>("Data")]
static class ListBoxStories
{
    [FramePath]
    public static void Default(Ui ui)
    {
        var value = ui.State<UiKey>(default);
        ui.ListBox(
            realizeAll: true,
            items: Words.Roles,
            itemKey: Words.ByText,
            itemText: Words.Text,
            value: value.Value,
            onValueChange: value.Setter,
            width: 200
        );
    }

    [FramePath]
    public static void ManyItems(Ui ui)
    {
        var value = ui.State<UiKey>(default);
        ui.ListBox(
            items: Words.Letters,
            itemKey: Words.ByText,
            itemText: Words.Text,
            value: value.Value,
            onValueChange: value.Setter,
            width: 200,
            height: 140
        );
    }

    [FramePath]
    public static void Filtered(Ui ui)
    {
        var words = ui.Remember(static () => new FilteredWords());
        var value = ui.State<UiKey>(default);
        using (ui.Box(direction: Direction.Column, gap: 8, width: 200))
        {
            ui.Switch(
                value: words.ShortOnly,
                onValueChange: words.Toggled,
                label: "Short words only"
            );
            ui.ListBox(
                items: words.Shown,
                itemKey: Words.ByText,
                itemText: Words.Text,
                value: value.Value,
                onValueChange: value.Setter,
                emptyText: "No matches",
                height: 140
            );
        }
    }

    [FramePath]
    public static void Empty(Ui ui) =>
        ui.ListBox(
            items: Words.None,
            itemKey: Words.ByText,
            itemText: Words.Text,
            emptyText: "No members yet",
            width: 200,
            height: 120
        );

    [FramePath]
    public static void InOverlay(Ui ui)
    {
        var t = ui.Theme;
        var value = ui.State<UiKey>("Saved account");
        using (
            ui.Box(
                direction: Direction.Column,
                width: 200,
                padding: 4,
                background: t.Color(ColorToken.BgOverlay),
                border: new Border(1f, t.Color(ColorToken.BorderOverlay)),
                radius: t.Radius(RadiusToken.Md),
                shadow: new BoxShadow(
                    0f,
                    2f,
                    10f,
                    0f,
                    t.Color(ColorToken.BgShadow).WithOpacity(0.6f)
                )
            )
        )
            ui.ListBox(
                realizeAll: true,
                items: Words.Accounts,
                itemKey: Words.ByText,
                itemText: Words.Text,
                value: value.Value,
                onValueChange: value.Setter
            );
    }

    [FramePath]
    public static void Reorder(Ui ui)
    {
        var jobs = ui.Remember(static () => Words.Of(["Engineer", "Designer", "Writer", "Tester"]));
        var value = ui.State<UiKey>(default);
        ui.ListBox(
            realizeAll: true,
            items: ItemsSource.From(jobs),
            itemKey: Words.ByText,
            itemText: Words.Text,
            value: value.Value,
            onValueChange: value.Setter,
            reorderable: true,
            onReorder: Callback<ListReorder>.Of(
                jobs,
                static (j, move) => j.Move(move.From, move.To)
            ),
            width: 200
        );
    }
}

[Stories<ListBox<Role>>("Data")]
static class RoleListBoxStories
{
    [FramePath]
    public static void SelectionAndDisabled(Ui ui)
    {
        var value = ui.State<UiKey>("Designer");
        ui.ListBox(
            items: Role.All,
            itemKey: Role.ByName,
            itemText: Role.Named,
            itemDisabled: Role.Locked,
            value: value.Value,
            onValueChange: value.Setter,
            width: 200,
            height: 140
        );
    }
}

[Stories<ListBox<SizedRow>>("Data")]
static class SizedListBoxStories
{
    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(gap: 16))
        {
            Sized(ui, SizedRow.Xs, ComponentSize.Xs, key: 0);
            Sized(ui, SizedRow.Sm, ComponentSize.Sm, key: 1);
            Sized(ui, SizedRow.Md, null, key: 2);
            Sized(ui, SizedRow.Lg, ComponentSize.Lg, key: 3);
        }
    }

    [FramePath]
    static void Sized(
        Ui ui,
        IItemsSource<SizedRow> rows,
        ComponentSize? size,
        UiKey key,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    ) =>
        ui.ListBox(
            realizeAll: true,
            items: rows,
            itemKey: SizedRow.ById,
            itemText: SizedRow.Named,
            value: 0,
            size: size,
            width: 140,
            key: key,
            file: file,
            line: line
        );
}

[Stories<ListBox<Holding>>("Data")]
static class HoldingListBoxStories
{
    static readonly Slot<Holding> Columns = Slot<Holding>.Of(
        static (ui, holding, _) =>
        {
            var t = ui.Theme;
            using (ui.Box(align: Align.Center, grow: 1f))
            {
                ui.Text(holding.Name, grow: 1f);
                ui.Text(holding.Kind, color: t.Color(ColorToken.FgMuted), width: 72f);
                ui.Text(holding.Times, textAlign: TextAlign.Right, width: 40f);
            }
        }
    );

    [FramePath]
    public static void Virtualized(Ui ui)
    {
        var value = ui.State<UiKey>(Holding.Many.ItemAt(2).Name);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            Caption(
                ui,
                "Ten thousand rows: only those in view, and half a viewport either side, have containers, reused as it scrolls"
            );
            using (Sunken(ui, width: 360))
                ui.ListBox(
                    items: Holding.Many,
                    itemKey: Holding.ByName,
                    itemText: Holding.Named,
                    item: Columns,
                    value: value.Value,
                    onValueChange: value.Setter,
                    height: 318
                );
        }
    }

    [FramePath]
    public static void MultipleSelection(Ui ui)
    {
        var chosen = ui.Remember(static () => new SelectionSet());
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            Caption(
                ui,
                "SelectionMode Multiple: a press, or Space on the item the arrows reached, adds or removes an item"
            );
            using (Sunken(ui, width: 280))
                ui.ListBox(
                    realizeAll: true,
                    items: Holding.Shelf,
                    itemKey: Holding.ByName,
                    itemText: Holding.Named,
                    selectionMode: SelectionMode.Multiple,
                    selection: chosen,
                    onSelectionChange: Callback<SelectionChange>.Of(
                        chosen,
                        static (set, change) => set.Apply(change, Holding.Shelf, Holding.ByName)
                    )
                );
        }
    }

    [FramePath]
    public static void ItemStates(Ui ui)
    {
        var t = ui.Theme;
        var value = ui.State<UiKey>(Holding.Shelf.ItemAt(1).Name);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            Caption(
                ui,
                "A container template reading IsSelected, :pointerover, :pressed, :focus and :disabled; the every-state toggle above holds each state"
            );
            using (
                ui.Box(
                    direction: Direction.Column,
                    width: 260,
                    padding: 4,
                    background: t.Color(ColorToken.BgSunken)
                )
            )
                ui.ListBox(
                    realizeAll: true,
                    items: Holding.Shelf,
                    itemKey: Holding.ByName,
                    itemText: Holding.Named,
                    value: value.Value,
                    onValueChange: value.Setter
                );
        }
    }

    [FramePath]
    static Scope Sunken(
        Ui ui,
        float width,
        UiKey key = default,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            direction: Direction.Column,
            width: width,
            background: t.Color(ColorToken.BgSunken),
            border: new Border(1f, t.Color(ColorToken.Border)),
            key: key,
            file: file,
            line: line
        );
    }

    [FramePath]
    internal static void Caption(
        Ui ui,
        string text,
        UiKey key = default,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "",
        [System.Runtime.CompilerServices.CallerLineNumber] int line = 0
    ) =>
        ui.Text(
            text,
            color: ui.Theme.Color(ColorToken.FgMuted),
            fontWeight: ui.Theme.Weight(WeightToken.Interface),
            wrap: true,
            key: key,
            file: file,
            line: line
        );
}

[Stories<ListBox.Row>("Data")]
static class ListBoxRowStories
{
    [FramePath]
    public static void States(Ui ui)
    {
        var muted = ui.Theme.Color(ColorToken.FgMuted);
        using (ui.Box(direction: Direction.Column, gap: 4, width: 240))
        {
            using (ui.ListBoxRow())
                ui.Text("Rest", color: muted);
            using (ui.Box(direction: Direction.Column, forceStates: InputStates.Hovered))
            using (ui.ListBoxRow())
                ui.Text("Highlighted", color: muted);
            using (ui.Box(direction: Direction.Column, forceStates: InputStates.Pressed))
            using (ui.ListBoxRow())
                ui.Text("Held", color: muted);
            using (ui.ListBoxRow(selected: true))
                ui.Text("Selected: muted text takes the accent", wrap: true, grow: 1f);
            using (ui.ListBoxRow(disabled: true))
                ui.Text("Disabled");
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(direction: Direction.Column, gap: 4, width: 240))
        {
            using (ui.ListBoxRow(size: ComponentSize.Xxs))
                ui.Text("Xxs");
            using (ui.ListBoxRow(size: ComponentSize.Sm))
                ui.Text("Sm");
            using (ui.ListBoxRow())
                ui.Text("Md, the default");
            using (ui.ListBoxRow(size: ComponentSize.Lg))
                ui.Text("Lg");
        }
    }
}

static class Words
{
    public static readonly IItemsSource<string> Roles = ItemsSource.From(
        new[] { "Engineer", "Designer", "Writer" }
    );

    public static readonly IItemsSource<string> Letters = ItemsSource.From(
        new[] { "Alpha", "Beta", "Gamma", "Delta", "Epsilon", "Zeta", "Eta", "Theta" }
    );

    public static readonly IItemsSource<string> Accounts = ItemsSource.From(
        new[] { "Saved account", "Another account", "Third account" }
    );

    public static readonly IItemsSource<string> None = ItemsSource.From(Array.Empty<string>());

    public static readonly KeyOf<string> ByText = static text => text;

    public static readonly TextOf<string> Text = static text => text;

    public static ObservableList<string> Of(ReadOnlySpan<string> words)
    {
        var list = new ObservableList<string>(words.Length);
        foreach (var word in words)
            list.Add(word);
        return list;
    }
}

/// <summary>A role a member may take, unless it is locked.</summary>
sealed record Role(string Name, bool IsLocked)
{
    public static readonly IItemsSource<Role> All = ItemsSource.From(
        new Role[]
        {
            new("Engineer", false),
            new("Designer", false),
            new("Writer (locked)", true),
            new("Tester", false),
        }
    );

    public static readonly KeyOf<Role> ByName = static role => role.Name;

    public static readonly TextOf<Role> Named = static role => role.Name;

    public static readonly Func<Role, bool> Locked = static role => role.IsLocked;
}

/// <summary>A row of a sized list, two of which read alike.</summary>
sealed record SizedRow(int Id, string Text)
{
    public static readonly IItemsSource<SizedRow> Xs = Two("Xs row");
    public static readonly IItemsSource<SizedRow> Sm = Two("Sm row");
    public static readonly IItemsSource<SizedRow> Md = Two("Md row");
    public static readonly IItemsSource<SizedRow> Lg = Two("Lg row");

    public static readonly KeyOf<SizedRow> ById = static row => row.Id;

    public static readonly TextOf<SizedRow> Named = static row => row.Text;

    static IItemsSource<SizedRow> Two(string text) =>
        ItemsSource.From(new SizedRow[] { new(0, text), new(1, text) });
}

/// <summary>One title on a library's shelves and how many copies it holds.</summary>
sealed record Holding(string Name, string Kind, int Count)
{
    public string Times { get; } = "×" + Count;

    public static readonly IItemsSource<Holding> Shelf = ItemsSource.From(
        new Holding[]
        {
            new("Field guide", "Book", 1),
            new("City atlas", "Map", 1),
            new("Morning edition", "Paper", 12),
            new("Harbour at dusk", "Film", 3),
            new("Jazz record", "Record", 7),
        }
    );

    public static readonly IItemsSource<Holding> Many = ItemsSource.From(Holdings(10_000));

    public static readonly KeyOf<Holding> ByName = static holding => holding.Name;

    public static readonly TextOf<Holding> Named = static holding => holding.Name;

    static Holding[] Holdings(int count)
    {
        string[] kinds = ["Book", "Map", "Paper", "Film", "Record"];
        var holdings = new Holding[count];
        for (var i = 0; i < holdings.Length; i++)
            holdings[i] = new Holding($"Item {i + 1:N0}", kinds[i % kinds.Length], i % 99 + 1);
        return holdings;
    }
}

[Stories<ListBox<GridEntry>>("Data")]
static class GridListBoxStories
{
    static readonly Slot<GridEntry> Card = Slot<GridEntry>.Of(
        static (ui, entry, _) =>
        {
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Center,
                    justify: Justify.Center,
                    gap: 4,
                    grow: 1f
                )
            )
            {
                ui.Icon(glyph: entry.Icon, size: 24f);
                ui.Text(
                    entry.Name,
                    variant: TextVariant.Caption,
                    fontWeight: ui.Theme.Weight(WeightToken.Interface)
                );
            }
        }
    );

    static readonly Slot<GridEntry> Line = Slot<GridEntry>.Of(
        static (ui, entry, _) =>
        {
            using (ui.Box(align: Align.Center, gap: 8))
            {
                ui.Icon(glyph: entry.Icon);
                ui.Text(entry.Name);
            }
        }
    );

    [FramePath]
    public static void Grid(Ui ui)
    {
        var value = ui.State<UiKey>(default);
        ui.ListBox(
            items: GridEntry.Few,
            itemKey: GridEntry.ByName,
            itemText: GridEntry.Named,
            card: Card,
            layout: ListLayout.Grid,
            itemWidth: 72,
            itemHeight: 72,
            value: value.Value,
            onValueChange: value.Setter,
            width: 320,
            height: 200
        );
    }

    [FramePath]
    public static void GridAsRows(Ui ui)
    {
        var value = ui.State<UiKey>(default);
        ui.ListBox(
            items: GridEntry.Few,
            itemKey: GridEntry.ByName,
            itemText: GridEntry.Named,
            item: Line,
            card: Card,
            value: value.Value,
            onValueChange: value.Setter,
            width: 240,
            height: 200
        );
    }

    [FramePath]
    public static void GridEmpty(Ui ui) =>
        ui.ListBox(
            items: GridEntry.None,
            itemKey: GridEntry.ByName,
            itemText: GridEntry.Named,
            card: Card,
            layout: ListLayout.Grid,
            emptyText: "Nothing in this category",
            width: 240,
            height: 160
        );

    [FramePath]
    public static void ManyCards(Ui ui)
    {
        var value = ui.State<UiKey>(GridEntry.Many.ItemAt(0).Name);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            HoldingListBoxStories.Caption(
                ui,
                "A thousand cards filling the width: a container per slot in view, handed new items as it scrolls"
            );
            ui.ListBox(
                items: GridEntry.Many,
                itemKey: GridEntry.ByName,
                itemText: GridEntry.Named,
                card: Card,
                layout: ListLayout.Grid,
                itemWidth: 72,
                itemHeight: 72,
                fillWidth: true,
                value: value.Value,
                onValueChange: value.Setter,
                width: 400,
                height: 300
            );
        }
    }

    [FramePath]
    public static void CardStates(Ui ui)
    {
        var card = ui.State<UiKey>(GridEntry.Few.ItemAt(1).Name);
        var row = ui.State<UiKey>(GridEntry.Few.ItemAt(1).Name);
        using (ui.Box(gap: 16))
        {
            ui.ListBox(
                items: GridEntry.Few,
                itemKey: GridEntry.ByName,
                itemText: GridEntry.Named,
                card: Card,
                layout: ListLayout.Grid,
                itemWidth: 64,
                itemHeight: 64,
                value: card.Value,
                onValueChange: card.Setter,
                width: 260,
                height: 180
            );
            ui.ListBox(
                items: GridEntry.Few,
                itemKey: GridEntry.ByName,
                itemText: GridEntry.Named,
                item: Line,
                value: row.Value,
                onValueChange: row.Setter,
                width: 200,
                height: 180
            );
        }
    }

    [FramePath]
    public static void Columns(Ui ui)
    {
        var value = ui.State<UiKey>(default);
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            HoldingListBoxStories.Caption(
                ui,
                "Exactly four columns across the width, each card keeping its aspect as it widens"
            );
            ui.ListBox(
                items: GridEntry.Few,
                itemKey: GridEntry.ByName,
                itemText: GridEntry.Named,
                card: Card,
                layout: ListLayout.Grid,
                itemWidth: 72,
                itemHeight: 60,
                columns: 4,
                keepAspect: true,
                value: value.Value,
                onValueChange: value.Setter,
                width: 360,
                height: 200
            );
        }
    }

    [FramePath]
    public static void VisibleRange(Ui ui)
    {
        var shown = ui.State(new ItemRange(0, -1));
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            HoldingListBoxStories.Caption(
                ui,
                "The view model holds a preview for only the cards on screen, told which as the grid scrolls"
            );
            using (ui.Box(gap: 4))
            {
                ui.Text(
                    ui.Format($"Previews for cards {shown.Value.First + 1}"),
                    fontWeight: ui.Theme.Weight(WeightToken.Interface)
                );
                ui.Text(
                    ui.Format($"to {shown.Value.Last + 1}"),
                    fontWeight: ui.Theme.Weight(WeightToken.Interface)
                );
            }
            ui.ListBox(
                items: GridEntry.Many,
                itemKey: GridEntry.ByName,
                itemText: GridEntry.Named,
                card: Card,
                layout: ListLayout.Grid,
                itemWidth: 72,
                itemHeight: 72,
                fillWidth: true,
                onVisibleRangeChange: shown.Setter,
                width: 400,
                height: 300
            );
        }
    }
}

/// <summary>One entry of a list shown as a grid or as rows.</summary>
sealed record GridEntry(string Name, IconGlyph Icon)
{
    public static readonly IItemsSource<GridEntry> Few = ItemsSource.From(
        new GridEntry[]
        {
            new("Shield", Gallery.Icons.Shield),
            new("Key", Gallery.Icons.Key),
            new("Map", Gallery.Icons.Map),
            new("Gift", Gallery.Icons.Gift),
            new("Heart", Gallery.Icons.Heart),
            new("Package", Gallery.Icons.Package),
            new("Star", Gallery.Icons.Star),
            new("Box", Gallery.Icons.Box),
        }
    );

    public static readonly IItemsSource<GridEntry> Many = ItemsSource.From(Entries(1_000));

    public static readonly IItemsSource<GridEntry> None = ItemsSource.From(
        Array.Empty<GridEntry>()
    );

    public static readonly KeyOf<GridEntry> ByName = static entry => entry.Name;

    public static readonly TextOf<GridEntry> Named = static entry => entry.Name;

    static GridEntry[] Entries(int count)
    {
        IconGlyph[] icons =
        [
            Gallery.Icons.Shield,
            Gallery.Icons.Key,
            Gallery.Icons.Map,
            Gallery.Icons.Gift,
            Gallery.Icons.Heart,
        ];
        var entries = new GridEntry[count];
        for (var i = 0; i < entries.Length; i++)
            entries[i] = new GridEntry($"Item {i + 1}", icons[i % icons.Length]);
        return entries;
    }
}

/// <summary>Words a filter hides in place: the list keeps every word, and a toggle changes only the rows it
/// shows.</summary>
sealed partial class FilteredWords : ObservableObject
{
    [ObservableProperty]
    public partial bool ShortOnly { get; set; }

    public IItemsSource<string> Shown { get; }

    public Callback<bool> Toggled { get; }

    public FilteredWords()
    {
        var words = Words.Of([
            "Alpha",
            "Beta",
            "Gamma",
            "Delta",
            "Epsilon",
            "Zeta",
            "Eta",
            "Theta",
        ]);
        Shown = ItemsSource.Matching<string>(
            words.ToView().Filter(word => !ShortOnly || word.Length <= 4)
        );
        Toggled = Callback<bool>.Of(this, static (model, on) => model.ShortOnly = on);
    }
}
