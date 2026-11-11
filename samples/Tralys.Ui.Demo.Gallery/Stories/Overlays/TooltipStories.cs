using Tralys.Ui.Components;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Tooltip>("Overlays")]
static class TooltipStories
{
    static readonly Slot Rich = Slot.Of(static ui =>
    {
        using (ui.Box(direction: Direction.Column, gap: 4))
        {
            ui.Text(
                "Rich content",
                variant: TextVariant.Heading,
                fontWeight: ui.Theme.Weight(WeightToken.Interface),
                wrap: true
            );
            ui.Text(
                "The content root asks for Sm; the frame supplies the chrome.",
                color: ui.Theme.Color(ColorToken.FgMuted),
                fontWeight: ui.Theme.Weight(WeightToken.Interface),
                wrap: true
            );
        }
    });

    [FramePath]
    public static void Default(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(direction: Direction.Column, gap: 8))
        {
            ui.Text(
                "Hover elements to see tooltips",
                color: t.Color(ColorToken.FgMuted),
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true
            );
            using (ui.Box(gap: 8, align: Align.Start))
            {
                using (ui.Tooltip(text: "This is a tooltip on a button"))
                    ui.Button(text: "Hover me");
                using (ui.Tooltip(text: "Info icon with tooltip"))
                    ui.IconButton(icon: Icons.Info);
                using (ui.Tooltip(text: "Tooltip on a border"))
                using (ui.Box(padding: 8, background: t.Color(ColorToken.BgSunken)))
                    ui.Text(
                        "Border with tooltip",
                        fontWeight: t.Weight(WeightToken.Interface),
                        wrap: true
                    );
            }
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        using (ui.Box(gap: 8, align: Align.Start))
        {
            using (
                ui.Tooltip(
                    text: "Extra small tooltips wrap at the narrowest measure, so a long sentence like this one breaks early",
                    size: ComponentSize.Xs
                )
            )
                ui.Button(text: "Xs");
            using (
                ui.Tooltip(
                    text: "Small tooltips wrap at the small measure, so a long sentence like this one breaks a little later",
                    size: ComponentSize.Sm
                )
            )
                ui.Button(text: "Sm");
            using (
                ui.Tooltip(
                    text: "Medium is the default measure, wide enough for a short paragraph like this one"
                )
            )
                ui.Button(text: "Md");
            using (
                ui.Tooltip(
                    text: "Large tooltips take larger text and the widest measure for longer descriptions like this one",
                    size: ComponentSize.Lg
                )
            )
                ui.Button(text: "Lg");
            using (ui.Tooltip(content: Rich, size: ComponentSize.Sm))
                ui.Button(text: "Rich");
        }
    }

    [FramePath]
    public static void Placements(Ui ui)
    {
        using (ui.Box(width: 360, height: 160, align: Align.Center, justify: Justify.Center))
        using (ui.Box(gap: 8))
        {
            using (ui.Tooltip(text: "Top", placement: PlacementMode.Top))
                ui.Button(text: "Top");
            using (ui.Tooltip(text: "Bottom", placement: PlacementMode.Bottom))
                ui.Button(text: "Bottom");
            using (ui.Tooltip(text: "LeftStart", placement: PlacementMode.LeftStart))
                ui.Button(text: "Left");
            using (ui.Tooltip(text: "RightStart", placement: PlacementMode.RightStart))
                ui.Button(text: "Right");
        }
    }

    [FramePath]
    public static void Nested(Ui ui)
    {
        var t = ui.Theme;
        using (
            ui.Tooltip(text: "The panel's tooltip comes back when the pointer leaves the button")
        )
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 8,
                width: 320,
                padding: 12,
                background: t.Color(ColorToken.BgSunken)
            )
        )
        {
            ui.Text(
                "Hover the panel, then the button, then the panel again",
                color: t.Color(ColorToken.FgMuted),
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true
            );
            using (ui.Tooltip(text: "The button's own tooltip", alignSelf: AlignSelf.Start))
                ui.Button(text: "Inner");
        }
    }
}
