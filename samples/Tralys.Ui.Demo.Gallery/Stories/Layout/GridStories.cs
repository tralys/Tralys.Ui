using System.Runtime.CompilerServices;
using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Grid>("Layout")]
static class GridStories
{
    static readonly string[] Days = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];

    [FramePath]
    public static void Repeat(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("Repeat(4, Fr(1)) columns: equal cells, as many rows as the children need");
            using (
                ui.Grid(
                    columns: GridTracks.Repeat(4, Track.Fr(1)),
                    rows: GridTracks.Repeat(2, Track.Fr(1)),
                    width: 400
                )
            )
            {
                for (var i = 0; i < 6; i++)
                {
                    using (ui.Cell(margin: 2, key: i))
                        ui.Text(Days[i], wrap: true);
                }
                using (ui.Cell(margin: 2))
                    ui.Text("A longer seventh", wrap: true);
            }
        }
    }

    [FramePath]
    public static void FirstColumn(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("Seven columns with firstColumn 4, as a month that starts on a Thursday");
            using (
                ui.Grid(
                    columns: GridTracks.Repeat(7, Track.Fr(1)),
                    rows: GridTracks.Repeat(2, Track.Fr(1)),
                    firstColumn: 4,
                    width: 350
                )
            )
            {
                for (var i = 0; i < Days.Length; i++)
                {
                    using (ui.Cell(margin: 2, key: i))
                        ui.Text(Days[i], wrap: true);
                }
            }
        }
    }

    [FramePath]
    public static void Sizing(Ui ui)
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
                "Columns 80, Auto, Fr(1) and Fr(2) across 560; rows Auto, 48 and Fr(1) down 200"
            );
            using (
                ui.Grid(
                    columns: new(80, Track.Auto, Track.Fr(1), Track.Fr(2)),
                    rows: new(Track.Auto, 48, Track.Fr(1)),
                    width: 560,
                    height: 200
                )
            )
            {
                using (ui.Cell(column: 1, row: 1))
                    ui.Text("80");
                using (ui.Cell(column: 2, row: 1))
                    ui.Text("Auto fits its text");
                using (ui.Cell(column: 3, row: 1))
                    ui.Text("1fr");
                using (ui.Cell(column: 4, row: 1))
                    ui.Text("2fr");
                using (ui.Cell(column: 1, row: 2))
                    ui.Text("48");
                using (ui.Cell(column: 2, row: 2)) { }
                using (ui.Cell(column: 3, row: 2)) { }
                using (ui.Cell(column: 4, row: 2)) { }
                using (ui.Cell(column: 1, row: 3))
                    ui.Text("1fr");
                using (ui.Cell(column: 2, row: 3)) { }
                using (ui.Cell(column: 3, row: 3)) { }
                using (ui.Cell(column: 4, row: 3)) { }
            }
        }
    }

    [FramePath]
    public static void Spans(Ui ui)
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
                "Cells spanning columns and rows; a span grows only the Auto tracks it covers"
            );
            using (
                ui.Grid(
                    columns: new(Track.Auto, Track.Fr(1), Track.Fr(1)),
                    rows: new(Track.Auto, Track.Auto, Track.Auto),
                    width: 480
                )
            )
            {
                using (ui.Cell(column: 1, row: 1, columnSpan: 3))
                    ui.Text("ColumnSpan 3");
                using (ui.Cell(column: 1, row: 2, rowSpan: 2))
                    ui.Text("RowSpan 2");
                using (ui.Cell(column: 2, row: 2))
                    ui.Text("1,1");
                using (ui.Cell(column: 3, row: 2))
                    ui.Text("1,2");
                using (ui.Cell(column: 2, row: 3, columnSpan: 2))
                    ui.Text("Row 2, ColumnSpan 2");
            }
        }
    }

    [FramePath]
    public static void LabelColumn(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("Rows of one grid share their Auto label column");
            using (ui.Grid(columns: new(Track.Auto, Track.Fr(1)), rowGap: 4, width: 420))
            {
                using (ui.Cell(column: 1, row: 1))
                    ui.Text("Name");
                using (ui.Cell(column: 2, row: 1))
                    ui.Text("Aria Vell");
                using (ui.Cell(column: 1, row: 2))
                    ui.Text("Team");
                using (ui.Cell(column: 2, row: 2))
                    ui.Text("Platform");
                using (ui.Cell(column: 1, row: 3))
                    ui.Text("Last seen online");
                using (ui.Cell(column: 2, row: 3))
                    ui.Text("Yesterday");
            }
        }
    }

    [FramePath]
    public static void MinMax(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("Fr columns held by MinMax(160, Fr(1)) and MinMax(0, 120) across 400");
            using (
                ui.Grid(
                    columns: new(Track.MinMax(160, Track.Fr(1)), Track.MinMax(0, 120), Track.Fr(1)),
                    width: 400
                )
            )
            {
                using (ui.Cell(column: 1))
                    ui.Text("Min 160");
                using (ui.Cell(column: 2))
                    ui.Text("Max 120");
                using (ui.Cell(column: 3))
                    ui.Text("The rest");
            }
        }
    }

    [FramePath]
    public static void Dock(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Top, Left, Right and Bottom around the edges in that order; the middle cell takes what is left"
            );
            using (
                ui.Grid(
                    columns: new(Track.Auto, Track.Fr(1), Track.Auto),
                    rows: new(Track.Auto, Track.Fr(1), Track.Auto),
                    width: 480,
                    height: 260
                )
            )
            {
                using (ui.Cell(column: 1, row: 1, columnSpan: 3))
                    ui.Text("Top");
                using (ui.Cell(column: 1, row: 2, rowSpan: 2))
                    ui.Text("Left");
                using (ui.Cell(column: 3, row: 2, rowSpan: 2))
                    ui.Text("Right");
                using (ui.Cell(column: 2, row: 3))
                    ui.Text("Bottom");
                using (
                    ui.Box(
                        align: Align.Center,
                        justify: Justify.Center,
                        column: 2,
                        row: 2,
                        background: t.Color(ColorToken.BgAccentMuted),
                        border: new Border(1, t.Color(ColorToken.BorderAccent))
                    )
                )
                    ui.Text("Fill");
            }
        }
    }

    [FramePath]
    public static void Spacer(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "An empty box draws nothing: a fixed spacer, and an Fr column that pushes the last item right"
            );
            using (
                ui.Grid(
                    columns: new(Track.Auto, Track.Auto, Track.Auto, Track.Fr(1), Track.Auto),
                    width: 420,
                    background: t.Color(ColorToken.BgSunken)
                )
            )
            {
                using (Accented(ui, 1))
                    ui.Text("One");
                using (ui.Box(column: 2, width: 32)) { }
                using (Accented(ui, 3))
                    ui.Text("Two");
                using (ui.Box(column: 4)) { }
                using (Accented(ui, 5))
                    ui.Text("Last");
            }
        }
    }

    [FramePath]
    static Scope Accented(
        Ui ui,
        int column,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            padding: new Thickness(8, 4, 8, 4),
            background: t.Color(ColorToken.BgAccentMuted),
            border: new Border(1, t.Color(ColorToken.BorderAccent)),
            column: column,
            key: key,
            file: file,
            line: line
        );
    }

    [FramePath]
    public static void Cells(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Grid(
                columns: GridTracks.Repeat(3, Track.Fr(1)),
                gap: t.Spacing,
                width: 240,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            Plain(ui, "One");
            Plain(ui, "Two");
            Plain(ui, "Three");
            Plain(ui, "Collapsed", collapsed: true);
            Plain(ui, "Four");
            Plain(ui, "Five");
        }
    }

    [FramePath]
    static void Plain(
        Ui ui,
        string text,
        bool collapsed = false,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (
            ui.Box(
                padding: new Thickness(8, 4, 8, 4),
                background: ui.Theme.Color(ColorToken.BgRaised),
                collapsed: collapsed,
                key: key,
                file: file,
                line: line
            )
        )
            ui.Text(text);
    }

    [FramePath]
    public static void Hole(Ui ui)
    {
        var t = ui.Theme;
        var raised = t.Color(ColorToken.BgRaised);
        var host = ui.Ref();
        var hole = ui.Ref();
        var outer = host.Bounds;
        var inner = hole.Bounds;
        var inset =
            inner.Width > 0
                ? new Thickness(
                    inner.X - outer.X,
                    inner.Y - outer.Y,
                    outer.Right - inner.Right,
                    outer.Bottom - inner.Bottom
                )
                : default;
        using (
            ui.Grid(
                rows: new(Track.Fr(1)),
                grow: 1,
                alignSelf: AlignSelf.Stretch,
                fontWeight: t.Weight(WeightToken.Interface),
                nodeRef: host
            )
        )
        {
            using (
                ui.Box(
                    align: Align.Center,
                    justify: Justify.Center,
                    column: 1,
                    row: 1,
                    margin: inset,
                    background: t.Color(ColorToken.BgSunken)
                )
            )
                ui.Button(text: "Under the hole");
            using (
                ui.Grid(
                    columns: new(Track.Fr(1), Track.Fr(3), 120),
                    rows: new(48, Track.Fr(1), 64),
                    column: 1,
                    row: 1
                )
            )
            {
                using (ui.Box(column: 1, row: 1, columnSpan: 3, background: raised))
                    ui.Caption(
                        "The hole in this frame stays over the Fr(3) cell as the view resizes",
                        margin: 12
                    );
                using (
                    ui.Box(
                        align: Align.Center,
                        justify: Justify.Center,
                        column: 1,
                        row: 2,
                        background: raised
                    )
                )
                    ui.Button(text: "On the frame");
                using (ui.Box(column: 2, row: 2, nodeRef: hole)) { }
                using (ui.Box(column: 3, row: 2, background: raised)) { }
                using (ui.Box(column: 1, row: 3, columnSpan: 3, background: raised)) { }
            }
        }
    }
}
