using Tralys.Ui.Components;
using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Overlay.Frame>("Overlays")]
static class OverlayFrameStories
{
    [FramePath]
    public static void Insets(Ui ui)
    {
        var t = ui.Theme;
        using (ui.Box(gap: 24, align: Align.Start))
        {
            using (ui.OverlayFrame(width: 180))
            {
                ui.StoryRow(text: "A list's rows");
                ui.StoryRow(text: "carry their own padding", highlighted: true);
            }
            using (ui.OverlayFrame(width: 180, inset: OverlayInset.Text))
                ui.Text(
                    "A line of text, padded like a control of the frame's size",
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
            using (ui.OverlayFrame(width: 180, inset: OverlayInset.Block))
            using (ui.Box(direction: Direction.Column, gap: 4))
            {
                ui.Text(
                    "A block",
                    variant: TextVariant.Heading,
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
                ui.Text(
                    "padded evenly on every side",
                    color: t.Color(ColorToken.FgMuted),
                    fontWeight: t.Weight(WeightToken.Interface),
                    wrap: true
                );
            }
        }
    }

    [FramePath]
    public static void Sizes(Ui ui)
    {
        var weight = ui.Theme.Weight(WeightToken.Interface);
        using (ui.Box(gap: 24, align: Align.Start))
        {
            using (ui.OverlayFrame(inset: OverlayInset.Text, size: ComponentSize.Xs))
                ui.Text("Xs", fontWeight: weight, wrap: true);
            using (ui.OverlayFrame(inset: OverlayInset.Text, size: ComponentSize.Sm))
                ui.Text("Sm", fontWeight: weight, wrap: true);
            using (ui.OverlayFrame(inset: OverlayInset.Text))
                ui.Text("Md", fontWeight: weight, wrap: true);
            using (ui.OverlayFrame(inset: OverlayInset.Text, size: ComponentSize.Lg))
                ui.Text("Lg", fontWeight: weight, wrap: true);
        }
    }
}

internal readonly record struct StoryRowProps
{
    public TextValue Text { get; init; }

    public bool Highlighted { get; init; }

    public bool Muted { get; init; }
}

internal sealed class StoryRow : Component<StoryRowProps>
{
    protected override void Render(Ui ui, in StoryRowProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(ui.Use(Overlay.Frame.Context).Size);
        using (
            ui.Box(
                align: Align.Center,
                minHeight: m.Height,
                padding: m.PaddingX,
                radius: t.Radius(RadiusToken.Sm),
                background: p.Highlighted || ui.IsHovered
                    ? t.Color(ColorToken.StateHover)
                    : default(Paint)
            )
        )
            ui.Text(
                p.Text,
                color: t.Color(p.Muted ? ColorToken.FgMuted : ColorToken.Fg),
                fontWeight: t.Weight(WeightToken.Interface),
                wrap: true,
                grow: 1
            );
    }
}
