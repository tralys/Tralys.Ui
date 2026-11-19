using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.VirtualList>("Data")]
static class VirtualListStories
{
    sealed record Holding(string Name, string Kind, int Count);

    sealed record Book(string Name, int Chapters);

    sealed record Film(string Name, int Minutes);

    static readonly string[] NameList = ["Novel", "Poetry", "Essays", "Drama", "Memoir", "Comics"];

    static readonly IItemsSource<string> Names = ItemsSource.From(NameList);

    static readonly IItemsSource<Holding> Shelf = ItemsSource.From(
        new Holding[]
        {
            new("Field guide", "Book", 1),
            new("City atlas", "Map", 1),
            new("Morning edition", "Paper", 12),
            new("Harbour at dusk", "Film", 3),
            new("Jazz record", "Record", 7),
        }
    );

    static readonly IItemsSource<object> Mixed = ItemsSource.From(
        new object[]
        {
            new Book("Field guide", 12),
            new Film("Harbour at dusk", 50),
            new Book("Poetry primer", 9),
            new Film("Coastal walk", 30),
            "A plain string shows as text",
        }
    );

    static readonly IItemsSource<Holding> Many = ItemsSource.From(Holdings(10_000));

    static readonly KeyOf<string> ByText = static s => s;
    static readonly KeyOf<Holding> ByName = static h => h.Name;
    static readonly KeyOf<object> ByItemName = static item =>
        item switch
        {
            Book book => book.Name,
            Film film => film.Name,
            _ => (string)item,
        };

    static readonly Slot<string> Plain = Slot<string>.Of(
        static (ui, name, _) => ui.Text(name, fontWeight: ui.Theme.Weight(WeightToken.Content))
    );

    static readonly Slot<Holding> Row = Slot<Holding>.Of(
        static (ui, holding, _) =>
        {
            var t = ui.Theme;
            using (
                ui.Box(
                    direction: Direction.Column,
                    padding: new Thickness(8, 6, 8, 6),
                    border: new(new Thickness(0, 0, 0, 1), t.Color(ColorToken.BorderMuted))
                )
            )
            using (ui.Grid(columns: new(Track.Fr(1), 72, Track.Auto)))
            {
                ui.Text(holding.Name);
                ui.Text(holding.Kind, color: t.Color(ColorToken.FgMuted));
                ui.Text($"×{holding.Count}");
            }
        }
    );

    static readonly Slot<object> ByType = Slot<object>.Of(
        static (ui, item, _) =>
        {
            var t = ui.Theme;
            switch (item)
            {
                case Book book:
                    using (ui.Box(gap: 8, margin: new Thickness(0, 2, 0, 2)))
                    {
                        ui.Icon(glyph: Icons.Book, size: 16);
                        ui.Text(book.Name);
                        ui.Text($"{book.Chapters} chapters", color: t.Color(ColorToken.FgAccent));
                    }
                    break;
                case Film film:
                    using (ui.Box(gap: 8, margin: new Thickness(0, 2, 0, 2)))
                    {
                        ui.Icon(glyph: Icons.Film, size: 16);
                        ui.Text(film.Name);
                        ui.Text($"{film.Minutes} min", color: t.Color(ColorToken.FgSuccess));
                    }
                    break;
                default:
                    ui.Text((string)item, fontWeight: t.Weight(WeightToken.Content));
                    break;
            }
        }
    );

    static readonly Slot<string> Centred = Slot<string>.Of(
        static (ui, name, _) =>
        {
            using (
                ui.Box(
                    justify: Justify.Center,
                    margin: 2,
                    padding: new Thickness(8, 4, 8, 4),
                    background: ui.Theme.Color(ColorToken.BgRaised)
                )
            )
                ui.Text(name);
        }
    );

    static readonly Slot<Holding> Tile = Slot<Holding>.Of(
        static (ui, holding, _) =>
        {
            var t = ui.Theme;
            using (ui.Box(direction: Direction.Column, justify: Justify.Center))
            using (
                ui.Box(
                    direction: Direction.Column,
                    align: Align.Center,
                    justify: Justify.Center,
                    gap: 2,
                    width: 88,
                    height: 80,
                    margin: 4,
                    radius: t.Radius(RadiusToken.Md),
                    background: t.Color(ColorToken.BgRaised),
                    border: new(1, t.Color(ColorToken.Border))
                )
            )
            {
                ui.Text(holding.Name);
                ui.Text(holding.Kind, color: t.Color(ColorToken.FgMuted));
            }
        }
    );

    static Holding[] Holdings(int count)
    {
        string[] kinds = ["Book", "Map", "Paper", "Film", "Record"];
        var holdings = new Holding[count];
        for (var i = 0; i < holdings.Length; i++)
            holdings[i] = new Holding($"Item {i + 1:N0}", kinds[i % kinds.Length], i % 99 + 1);
        return holdings;
    }

    [FramePath]
    public static void Strings(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("Strings shown as text by a row slot, one under another");
            using (
                ui.Box(
                    direction: Direction.Column,
                    width: 200,
                    padding: 8,
                    background: t.Color(ColorToken.BgSunken)
                )
            )
                ui.VirtualList(
                    items: Names,
                    itemKey: ByText,
                    row: Plain,
                    realizeAll: true,
                    grow: 1
                );
        }
    }

    [FramePath]
    public static void RowSlot(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("A row slot builds each row from its item");
            using (ui.Box(direction: Direction.Column, width: 320))
                ui.VirtualList(items: Shelf, itemKey: ByName, row: Row, realizeAll: true, grow: 1);
        }
    }

    [FramePath]
    public static void Types(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "One row slot switches on each item's type; a string it doesn't know shows as text"
            );
            using (ui.Box(direction: Direction.Column, width: 320))
                ui.VirtualList(
                    items: Mixed,
                    itemKey: ByItemName,
                    row: ByType,
                    realizeAll: true,
                    grow: 1
                );
        }
    }

    [FramePath]
    public static void Layouts(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("The same names in a wrapping box and a three-column list");
            using (ui.Box(gap: 24, align: Align.Start))
            {
                using (ui.Box(wrap: FlexWrap.Wrap, width: 220))
                {
                    for (var i = 0; i < NameList.Length; i++)
                        Chip(ui, NameList[i], key: i);
                }
                using (ui.Box(direction: Direction.Column, width: 240))
                    ui.VirtualList(
                        items: Names,
                        itemKey: ByText,
                        row: Centred,
                        columns: 3,
                        realizeAll: true,
                        grow: 1
                    );
            }
        }
    }

    [FramePath]
    static void Chip(
        Ui ui,
        string name,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                margin: 2,
                padding: new Thickness(8, 4, 8, 4),
                radius: t.Radius(RadiusToken.Md),
                background: t.Color(ColorToken.BgRaised),
                key: key,
                file: file,
                line: line
            )
        )
            ui.Text(name);
    }

    [FramePath]
    public static void Horizontal(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "A horizontal list: ten thousand tiles across, realized only around the viewport"
            );
            var scroll = ui.ScrollHandle();
            using (
                ui.Box(
                    direction: Direction.Column,
                    width: 560,
                    height: 112,
                    background: t.Color(ColorToken.BgSunken)
                )
            )
            {
                ui.VirtualList(
                    items: Many,
                    itemKey: ByName,
                    row: Tile,
                    horizontal: true,
                    scroll: scroll,
                    grow: 1
                );
                ui.ScrollAreaBar(
                    handle: scroll,
                    shown: ui.IsHovered || ui.IsFocusWithin || ui.IsCapturing
                );
            }
        }
    }
}
