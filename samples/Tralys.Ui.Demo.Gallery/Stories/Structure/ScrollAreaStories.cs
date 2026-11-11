using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<ScrollArea>("Structure")]
static class ScrollAreaStories
{
    static readonly string[] Rows =
    [
        "Row 1 — scrollable content",
        "Row 2 — scrollable content",
        "Row 3 — scrollable content",
        "Row 4 — scrollable content",
        "Row 5 — scrollable content",
        "Row 6 — scrollable content",
        "Row 7 — scrollable content",
        "Row 8 — scrollable content",
    ];

    static readonly string[] Inner = ["Inner 1", "Inner 2", "Inner 3"];

    static readonly string[] Lines = ["Row 3", "Row 4", "Row 5", "Row 6", "Row 7", "Row 8"];

    static readonly string[] Buttons =
    [
        "Row 1",
        "Row 2",
        "Row 3",
        "Row 4",
        "Row 5",
        "Row 6",
        "Row 7",
        "Row 8",
    ];

    static readonly string[] Columns = ["A", "B", "C", "D", "E", "F"];

    static readonly string[] Cells =
    [
        "A1",
        "B1",
        "C1",
        "D1",
        "E1",
        "F1",
        "A2",
        "B2",
        "C2",
        "D2",
        "E2",
        "F2",
        "A3",
        "B3",
        "C3",
        "D3",
        "E3",
        "F3",
        "A4",
        "B4",
        "C4",
        "D4",
        "E4",
        "F4",
    ];

    static readonly GridTracks BoardColumns = GridTracks.Repeat(6, Track.Fr(1f));

    static readonly GridTracks BoardRows = GridTracks.Repeat(4, Track.Fr(1f));

    [FramePath]
    public static void Default(Ui ui)
    {
        var t = ui.Theme;
        var weight = t.Weight(WeightToken.Interface);
        using (ui.ScrollArea(width: 280, height: 160))
        using (ui.Box(direction: Direction.Column, gap: 16))
            for (var i = 0; i < Rows.Length; i++)
                using (
                    ui.Box(padding: 8, border: new Border(1f, t.Color(ColorToken.Border)), key: i)
                )
                    ui.Text(Rows[i], fontWeight: weight, wrap: true, grow: 1f);
    }

    [FramePath]
    public static void Nested(Ui ui)
    {
        var t = ui.Theme;
        var weight = t.Weight(WeightToken.Interface);
        using (ui.ScrollArea(horizontal: ScrollbarVisibility.Disabled, width: 280, height: 160))
        using (ui.Box(direction: Direction.Column, gap: 16))
        {
            ui.Text("Outer rows scroll vertically", fontWeight: weight, wrap: true);
            using (ui.ScrollArea(vertical: ScrollbarVisibility.Disabled, height: 48, id: "inner"))
            using (ui.Box(gap: 16))
                for (var i = 0; i < Inner.Length; i++)
                    using (
                        ui.Box(
                            width: 120,
                            padding: 8,
                            border: new Border(1f, t.Color(ColorToken.Border)),
                            key: i
                        )
                    )
                        ui.Text(Inner[i], fontWeight: weight, wrap: true, grow: 1f);
            for (var i = 0; i < Lines.Length; i++)
                ui.Text(Lines[i], fontWeight: weight, wrap: true, key: i);
        }
    }

    [FramePath]
    public static void Panning(Ui ui)
    {
        var t = ui.Theme;
        var weight = t.Weight(WeightToken.Interface);
        var board = ui.ScrollHandle();
        using (ui.Box(gap: 24))
        {
            using (ui.Box(direction: Direction.Column, gap: 4, width: 200))
            {
                ui.Text(
                    "A finger pans down the buttons",
                    color: t.Color(ColorToken.FgMuted),
                    fontWeight: weight,
                    wrap: true
                );
                using (
                    ui.ScrollArea(panning: Tralys.Ui.Panning.VerticalOnly, height: 160, id: "list")
                )
                using (ui.Box(direction: Direction.Column, align: Align.Start, gap: 4))
                    for (var i = 0; i < Buttons.Length; i++)
                        ui.Button(text: Buttons[i], key: i);
            }
            using (ui.Box(direction: Direction.Column, gap: 4, width: 280))
            {
                ui.Text(
                    "A mouse drag pans the board; its header follows",
                    color: t.Color(ColorToken.FgMuted),
                    fontWeight: weight,
                    wrap: true
                );
                using (
                    ui.ScrollArea(
                        horizontal: ScrollbarVisibility.Hidden,
                        vertical: ScrollbarVisibility.Disabled,
                        followScroll: board,
                        height: 20,
                        id: "header"
                    )
                )
                using (ui.Box())
                    for (var i = 0; i < Columns.Length; i++)
                        ui.Text(
                            Columns[i],
                            color: t.Color(ColorToken.FgStrong),
                            fontWeight: weight,
                            wrap: true,
                            width: 100,
                            shrink: 0f,
                            key: i
                        );
                using (ui.ScrollArea(scroll: board, dragToPan: true, height: 160, id: "board"))
                using (ui.Grid(columns: BoardColumns, rows: BoardRows, width: 600, height: 400))
                    for (var i = 0; i < Cells.Length; i++)
                        using (
                            ui.Box(
                                background: (i / 6 + i % 6) % 2 == 0
                                    ? t.Color(ColorToken.BgSubtle)
                                    : default(Paint),
                                border: new Border(1f, t.Color(ColorToken.Border)),
                                key: i
                            )
                        )
                            ui.Text(Cells[i], fontWeight: weight, wrap: true, grow: 1f);
            }
        }
    }

    [FramePath]
    public static void BothAxes(Ui ui)
    {
        var t = ui.Theme;
        using (ui.ScrollArea(width: 280, height: 160))
        using (
            ui.Box(
                width: 480,
                height: 320,
                padding: 12,
                border: new Border(1f, t.Color(ColorToken.Border)),
                background: t.Color(ColorToken.BgSunken),
                alignSelf: AlignSelf.Start
            )
        )
            ui.Text(
                "The presenter shows the part of this panel the viewer's offsets reach, clipped to its own bounds, and scrolls both ways",
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true,
                grow: 1f
            );
    }

    [FramePath]
    public static void Padded(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.ScrollArea(
                horizontal: ScrollbarVisibility.Disabled,
                vertical: ScrollbarVisibility.Visible,
                padding: 12,
                width: 280,
                height: 160
            )
        )
            ui.Text(
                "Padding sets the presenter in from the viewer's edge; the bar shows though nothing overflows",
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true
            );
    }
}
