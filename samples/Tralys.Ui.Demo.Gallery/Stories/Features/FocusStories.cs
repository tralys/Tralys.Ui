using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Focus>("Features")]
static class FocusStories
{
    static readonly string[] Twelve =
    [
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8",
        "9",
        "10",
        "11",
        "12",
    ];
    static readonly string[] Six = ["A", "B", "C", "D", "E", "F"];

    [FramePath]
    public static void Tab(Ui ui)
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
                "Click a tile, then Tab and Shift+Tab. tabIndex orders the first row; the dimmed tile is no Tab stop"
            );
            using (ui.Box())
            {
                ui.FocusTile(label: "Third", tabIndex: 3, margin: 3);
                ui.FocusTile(label: "First", tabIndex: 1, margin: 3);
                ui.FocusTile(label: "Second", tabIndex: 2, margin: 3);
                ui.FocusTile(label: "Skipped", tabIndex: -1, dimmed: true, margin: 3);
            }
            ui.Caption(
                "tabNavigation Once: Tab stops in the group once, at the tile last focused there"
            );
            using (Group(ui, Navigation.Once, default))
            {
                ui.FocusTile(label: "A", margin: 3);
                ui.FocusTile(label: "B", margin: 3);
                ui.FocusTile(label: "C", margin: 3);
            }
            ui.Caption("tabNavigation Cycle: Tab wraps inside the group and never leaves it");
            using (Group(ui, Navigation.Cycle, default))
            {
                ui.FocusTile(label: "X", margin: 3);
                ui.FocusTile(label: "Y", margin: 3);
                ui.FocusTile(label: "Z", margin: 3);
            }
        }
    }

    [FramePath]
    public static void Arrows(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Box(direction: Direction.Column, gap: 8, fontWeight: t.Weight(WeightToken.Interface))
        )
        {
            ui.Caption(
                "Click a tile, then the arrow keys: each moves to the nearest tile that way"
            );
            using (ui.Box(gap: 24, align: Align.Start))
            {
                using (ui.Box(direction: Direction.Column, gap: 4))
                {
                    ui.Text(
                        "arrowNavigation Cycle: wraps at the edges",
                        variant: TextVariant.Caption,
                        color: t.Color(ColorToken.FgMuted)
                    );
                    using (Group(ui, default, Navigation.Cycle))
                    using (ui.Grid(columns: GridTracks.Repeat(4, Track.Fr(1))))
                    {
                        for (var i = 0; i < Twelve.Length; i++)
                            ui.FocusTile(label: Twelve[i], margin: 3, key: i);
                    }
                }
                using (ui.Box(direction: Direction.Column, gap: 4))
                {
                    ui.Text(
                        "Contained: stops at the edges",
                        variant: TextVariant.Caption,
                        color: t.Color(ColorToken.FgMuted)
                    );
                    using (Group(ui, default, Navigation.Contained))
                    using (ui.Grid(columns: GridTracks.Repeat(3, Track.Fr(1))))
                    {
                        for (var i = 0; i < Six.Length; i++)
                            ui.FocusTile(label: Six[i], margin: 3, key: i);
                    }
                }
            }
        }
    }

    [FramePath]
    static Scope Group(
        Ui ui,
        Navigation tab,
        Navigation arrows,
        UiKey key = default,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0
    )
    {
        var t = ui.Theme;
        return ui.Box(
            padding: 6,
            radius: t.Radius(RadiusToken.Lg),
            border: new(1, t.Color(ColorToken.BorderMuted)),
            tabNavigation: tab,
            arrowNavigation: arrows,
            alignSelf: AlignSelf.Start,
            key: key,
            file: file,
            line: line
        );
    }
}

internal readonly record struct FocusTileProps
{
    public TextValue Label { get; init; }
    public int TabIndex { get; init; }
    public bool Dimmed { get; init; }
}

/// <summary>A focusable tile that outlines on hover and fills with the accent while focused.</summary>
internal sealed class FocusTile : Component<FocusTileProps>
{
    protected override void Render(Ui ui, in FocusTileProps p)
    {
        var t = ui.Theme;
        var focused = ui.IsFocused;
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: 88,
                height: 44,
                radius: t.Radius(RadiusToken.Md),
                background: t.Color(focused ? ColorToken.BgAccentMuted : ColorToken.BgRaised),
                border: new(
                    focused ? 2 : 1,
                    t.Color(
                        focused ? ColorToken.BorderFocus
                        : ui.IsHovered ? ColorToken.BorderHover
                        : ColorToken.Border
                    )
                ),
                opacity: p.Dimmed ? 0.5f : null,
                focusable: true,
                tabIndex: p.TabIndex
            )
        )
            ui.Text(p.Label);
    }
}
