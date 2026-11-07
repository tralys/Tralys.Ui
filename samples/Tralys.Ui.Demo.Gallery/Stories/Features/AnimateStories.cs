using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery.Stories;

[Stories<Subjects.Animate>("Features")]
static class AnimateStories
{
    static readonly IEasingFunction Linear = new KeySpline();

    [FramePath]
    public static void Transitions(Ui ui)
    {
        var t = ui.Theme;
        var md = t.Radius(RadiusToken.Md);
        var flipped = ui.Loop(3) >= 0.5f;
        var linear = ui.Animate(flipped ? 200 : 60, new Transition(0.6f, Linear));
        var eased = ui.Animate(
            flipped ? 200 : 60,
            new Transition(0.6f, Easings.Back(1, EasingMode.Out))
        );
        var delayed = ui.Animate(flipped ? 0.2f : 1, new Transition(0.4f, Linear, Delay: 0.4f));
        using (
            ui.Box(
                direction: Direction.Column,
                gap: 12,
                fontWeight: t.Weight(WeightToken.Interface)
            )
        )
        {
            ui.Caption(
                "Animate moves a value to each new target it is given: here a loop flips the targets every 1.5 s, and hovering a tile lifts it"
            );
            using (ui.Box(gap: 16))
            {
                using (
                    ui.Box(
                        width: linear,
                        height: 60,
                        radius: md,
                        background: t.Color(ColorToken.BgAccent)
                    )
                ) { }
                using (
                    ui.Box(
                        width: eased,
                        height: 60,
                        radius: md,
                        background: t.Color(ColorToken.BgSuccess)
                    )
                ) { }
                using (
                    ui.Box(
                        width: 60,
                        height: 60,
                        radius: md,
                        opacity: delayed,
                        background: t.Color(ColorToken.BgWarning)
                    )
                ) { }
            }
            using (ui.Box(gap: 8, align: Align.End))
            {
                for (var i = 0; i < 4; i++)
                    ui.HoverTile(key: i);
            }
        }
    }
}

internal readonly record struct HoverTileProps;

/// <summary>A tile that brightens and grows from 48 to 64 tall over 0.2 s while hovered.</summary>
internal sealed class HoverTile : Component<HoverTileProps>
{
    static readonly IEasingFunction Linear = new KeySpline();

    protected override void Render(Ui ui, in HoverTileProps p)
    {
        var t = ui.Theme;
        var hovered = ui.IsHovered;
        var quick = new Transition(0.2f, Linear);
        using (
            ui.Box(
                width: 72,
                height: ui.Animate(hovered ? 64 : 48, quick),
                opacity: ui.Animate(hovered ? 1 : 0.6f, quick),
                radius: t.Radius(RadiusToken.Md),
                background: t.Color(ColorToken.BgAccentMuted),
                border: new(1, t.Color(ColorToken.BorderAccent))
            )
        ) { }
    }
}
