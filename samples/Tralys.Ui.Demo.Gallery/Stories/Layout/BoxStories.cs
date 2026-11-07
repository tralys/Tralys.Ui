using System.Runtime.CompilerServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Box>("Layout")]
static class BoxStories
{
    static readonly string[] Supplies =
    [
        "Paper",
        "Folder",
        "Map of the north coast",
        "Tape",
        "Binder",
        "Clips",
        "Glue",
        "Envelopes",
        "Stamp",
    ];

    static readonly string[] Numbers = ["One", "Two", "Three", "Four", "Five", "Six", "Seven"];

    [FramePath]
    public static void Column(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 16,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
            Items(ui);
    }

    [FramePath]
    public static void Row(Ui ui)
    {
        using (ui.Box(gap: 16, fontWeight: ui.Theme.Weight(WeightToken.Interface)))
            Items(ui);
    }

    [FramePath]
    static void Items(
        Ui ui,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Fragment(key: key, file: file, line: line))
        {
            using (ui.Cell())
                ui.Text("Item 1");
            using (ui.Cell())
                ui.Text("Item 2");
            using (ui.Cell())
                ui.Text("Item 3");
        }
    }

    [FramePath]
    public static void Gap(Ui ui)
    {
        using (ui.Box(gap: 48, fontWeight: ui.Theme.Weight(WeightToken.Interface)))
        {
            Lettered(ui, 4, "Gap 4");
            Lettered(ui, 16, "Gap 16");
            Lettered(ui, 48, "Gap 48");
        }
    }

    [FramePath]
    static void Lettered(
        Ui ui,
        float gap,
        string caption,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Box(direction: Direction.Column, gap: gap, key: key, file: file, line: line))
        {
            ui.Caption(caption);
            using (ui.Cell())
                ui.Text("A");
            using (ui.Cell())
                ui.Text("B");
            using (ui.Cell())
                ui.Text("C");
        }
    }

    [FramePath]
    public static void CollapsedGap(Ui ui)
    {
        using (ui.Box(gap: 24, fontWeight: ui.Theme.Weight(WeightToken.Interface)))
        {
            Counted(ui, 0, "Gap 0", false);
            Counted(ui, 8, "Gap 8", false);
            Counted(ui, 8, "A collapsed child takes no gap", true);
        }
    }

    [FramePath]
    static void Counted(
        Ui ui,
        float gap,
        string caption,
        bool collapseTwo,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        using (ui.Box(direction: Direction.Column, gap: gap, key: key, file: file, line: line))
        {
            ui.Caption(caption);
            using (ui.Cell())
                ui.Text("One");
            using (ui.Cell(collapsed: collapseTwo))
                ui.Text("Two");
            using (ui.Cell())
                ui.Text("Three");
        }
    }

    [FramePath]
    public static void Stretch(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 12,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("In a row, children stretch to the tallest; in a column, across the widest");
            using (ui.Box(gap: 4))
            {
                using (ui.Cell())
                    ui.Text("Short");
                using (ui.Cell(height: 56))
                    ui.Text("56 tall");
                using (ui.Cell(alignSelf: AlignSelf.Start))
                    ui.Text("Top");
                using (ui.Cell(alignSelf: AlignSelf.End))
                    ui.Text("Bottom");
            }
            using (ui.Box(direction: Direction.Column, width: 240, gap: 4))
            {
                using (ui.Cell())
                    ui.Text("Stretched");
                using (ui.Cell(alignSelf: AlignSelf.Start))
                    ui.Text("Left");
                using (ui.Cell(alignSelf: AlignSelf.Center))
                    ui.Text("Center");
                using (ui.Cell(alignSelf: AlignSelf.End))
                    ui.Text("Right");
            }
        }
    }

    [FramePath]
    public static void Wrap(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("Children flow left to right and wrap at 320");
            using (
                ui.Box(
                    wrap: FlexWrap.Wrap,
                    width: 320,
                    background: ui.Theme.Color(ColorToken.BgSunken)
                )
            )
            {
                for (var i = 0; i < Supplies.Length; i++)
                    Chip(ui, Supplies[i], key: i);
            }
        }
    }

    [FramePath]
    public static void WrapColumn(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("A column, wrapping into columns at 120 tall");
            using (
                ui.Box(
                    direction: Direction.Column,
                    wrap: FlexWrap.Wrap,
                    alignContent: AlignContent.Start,
                    height: 120,
                    background: ui.Theme.Color(ColorToken.BgSunken)
                )
            )
            {
                for (var i = 0; i < Numbers.Length; i++)
                    Chip(ui, Numbers[i], key: i);
            }
        }
    }

    [FramePath]
    public static void WrapItemSize(Ui ui)
    {
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                fontWeight: ui.Theme.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption("Every child 96 wide and 40 tall gives each the same slot");
            using (
                ui.Box(
                    wrap: FlexWrap.Wrap,
                    width: 320,
                    background: ui.Theme.Color(ColorToken.BgSunken)
                )
            )
            {
                for (var i = 0; i < 5; i++)
                    Chip(ui, Supplies[i], width: 92, height: 36, key: i);
            }
        }
    }

    [FramePath]
    static void Chip(
        Ui ui,
        string text,
        Length width = default,
        Length height = default,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                direction: Direction.Column,
                padding: new Thickness(8, 4, 8, 4),
                background: t.Color(ColorToken.BgRaised),
                border: new Border(1, t.Color(ColorToken.Border)),
                radius: t.Radius(RadiusToken.Md),
                margin: 2,
                width: width,
                height: height,
                key: key,
                file: file,
                line: line
            )
        )
            ui.Text(text, trimming: TextTrimming.CharacterEllipsis);
    }

    [FramePath]
    public static void DockEdges(Ui ui)
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
                "Children start from the left, and a growing space pushes the last to the right"
            );
            using (ui.Box(width: 480, height: 80, background: ui.Theme.Color(ColorToken.BgSunken)))
            {
                using (ui.Cell())
                    ui.Text("One");
                using (ui.Cell())
                    ui.Text("Two");
                using (ui.Cell())
                    ui.Text("Three");
                using (ui.Box(grow: 1)) { }
                using (ui.Cell())
                    ui.Text("Right");
            }
        }
    }

    [FramePath]
    public static void Absolute(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Children placed by left and top, or by right and bottom from the far edges; later ones draw on top"
            );
            using (ui.Box(width: 420, height: 220, background: t.Color(ColorToken.BgSunken)))
            {
                Placed(ui, "Left 12, Top 12", 12, default, default, 12);
                Placed(ui, "Right 12, Top 12", default, 12, default, 12);
                Placed(ui, "Left 12, Bottom 12", 12, default, 12, default);
                Placed(ui, "Right 12, Bottom 12", default, 12, 12, default);
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 140,
                        top: 80,
                        width: 120,
                        height: 60,
                        background: t.Color(ColorToken.BgAccentMuted)
                    )
                ) { }
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: 180,
                        top: 100,
                        width: 120,
                        height: 60,
                        opacity: 0.8f,
                        background: t.Color(ColorToken.BgAccent)
                    )
                ) { }
            }
        }
    }

    [FramePath]
    static void Placed(
        Ui ui,
        string text,
        Length left,
        Length right,
        Length bottom,
        Length top,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                padding: new Thickness(8, 6, 8, 6),
                background: t.Color(ColorToken.BgRaised),
                border: new Border(1, t.Color(ColorToken.Border)),
                position: Position.Absolute,
                left: left,
                top: top,
                right: right,
                bottom: bottom,
                key: key,
                file: file,
                line: line
            )
        )
            ui.Text(text);
    }

    [FramePath]
    public static void AbsoluteOverflow(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "An absolute child takes no room, so it overflows its parent unless the parent clips it"
            );
            using (ui.Box(gap: 48))
            {
                Overflowing(ui, Overflow.Visible);
                Overflowing(ui, Overflow.Clip);
            }
        }
    }

    [FramePath]
    static void Overflowing(
        Ui ui,
        Overflow overflow,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        using (
            ui.Box(
                width: 160,
                height: 100,
                overflow: overflow,
                background: t.Color(ColorToken.BgSunken),
                key: key,
                file: file,
                line: line
            )
        )
        {
            using (
                ui.Box(
                    position: Position.Absolute,
                    left: 100,
                    top: 60,
                    width: 100,
                    height: 70,
                    background: t.Color(ColorToken.BgAccent)
                )
            ) { }
        }
    }

    [FramePath]
    public static void Margin(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("A child stretched across its parent's whole space, less its margin");
            using (
                ui.Box(
                    direction: Direction.Column,
                    width: 320,
                    height: 120,
                    background: t.Color(ColorToken.BgSunken)
                )
            )
            {
                using (
                    ui.Box(
                        align: Align.Center,
                        justify: Justify.Center,
                        grow: 1,
                        margin: 16,
                        background: t.Color(ColorToken.BgAccentMuted),
                        border: new Border(1, t.Color(ColorToken.BorderAccent))
                    )
                )
                    ui.Text("Child");
            }
        }
    }

    [FramePath]
    public static void Alignment(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption("One child in a 200×96 slot, per alignSelf and justifySelf");
            using (
                ui.Grid(
                    columns: GridTracks.Repeat(4, Track.Fr(1)),
                    rows: GridTracks.Repeat(2, Track.Fr(1))
                )
            )
            {
                using (Lane(ui, 0))
                {
                    using (Aligned(ui, AlignSelf.Start, AlignSelf.Start))
                        ui.Text("Left, Top");
                }
                using (Lane(ui, 1))
                {
                    using (Aligned(ui, AlignSelf.Center, AlignSelf.Center))
                        ui.Text("Center");
                }
                using (Lane(ui, 2))
                {
                    using (Aligned(ui, AlignSelf.End, AlignSelf.End))
                        ui.Text("Right, Bottom");
                }
                using (Lane(ui, 3))
                {
                    using (Aligned(ui, default, default))
                        ui.Text("Stretch");
                }
                using (Lane(ui, 4))
                {
                    using (Aligned(ui, default, default, margin: new Thickness(24, 12, 24, 12)))
                        ui.Text("Margin 24,12");
                }
                using (Lane(ui, 5))
                {
                    using (Aligned(ui, default, default, width: 80, height: 40))
                        ui.Text("80×40");
                }
                using (Lane(ui, 6))
                {
                    using (Aligned(ui, default, default, maxWidth: 120, maxHeight: 48))
                        ui.Text("Max 120×48");
                }
                using (Lane(ui, 7))
                {
                    using (Aligned(ui, AlignSelf.Start, default, minWidth: 240))
                        ui.Text("Min 240, clipped");
                }
            }
        }
    }

    [FramePath]
    static Scope Lane(
        Ui ui,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Grid(
            width: 200,
            height: 96,
            margin: new Thickness(0, 0, 8, 8),
            overflow: Overflow.Clip,
            background: t.Color(ColorToken.BgSunken),
            border: new Border(1, t.Color(ColorToken.Border)),
            key: key,
            file: file,
            line: line
        );
    }

    [FramePath]
    static Scope Aligned(
        Ui ui,
        AlignSelf horizontal,
        AlignSelf vertical,
        Thickness margin = default,
        Length width = default,
        Length height = default,
        Length minWidth = default,
        Length maxWidth = default,
        Length maxHeight = default,
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
            justifySelf: horizontal,
            alignSelf: vertical,
            margin: margin,
            width: width,
            height: height,
            minWidth: minWidth,
            maxWidth: maxWidth,
            maxHeight: maxHeight,
            key: key,
            file: file,
            line: line
        );
    }

    [FramePath]
    public static void QuarterTurns(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Quarter turns turn a box before layout, so its neighbours make room; a transform does not"
            );
            using (ui.Box(gap: 16, align: Align.Start))
            {
                using (Turnable(ui, 3, default))
                    ui.Text("Turned a quarter");
                using (Turnable(ui, 0, Transform.Rotate(30)))
                    ui.Text("Turned 30°");
                using (Turnable(ui, 0, Transform.Scale(1.5f)))
                    ui.Text("Half again as big");
                using (Turnable(ui, 0, Transform.Rotate(30)))
                    ui.Text("Rendered at 30°");
            }
            ui.Caption("A stretched box turned a quarter fills its 160×72 slot at 72×160");
            using (
                ui.Box(
                    direction: Direction.Column,
                    width: 160,
                    height: 72,
                    background: t.Color(ColorToken.BgSunken),
                    border: new Border(1, t.Color(ColorToken.Border))
                )
            )
            {
                using (Turnable(ui, 1, default, grow: 1))
                    ui.Text("Down");
            }
        }
    }

    [FramePath]
    static Scope Turnable(
        Ui ui,
        int quarterTurns,
        Transform transform,
        float grow = 0,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            quarterTurns: quarterTurns,
            transform: transform,
            grow: grow,
            padding: new Thickness(8, 4, 8, 4),
            background: t.Color(ColorToken.BgAccentMuted),
            border: new Border(1, t.Color(ColorToken.BorderAccent)),
            key: key,
            file: file,
            line: line
        );
    }
}
