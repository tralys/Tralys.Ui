using Tralys.Ui.Rendering;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct CloseButtonProps
{
    public ComponentSize? Size { get; init; }

    public bool Disabled { get; init; }

    public Callback OnClick { get; init; }
}

/// <summary>The round button that closes what it sits on: a cross on a disc of its own, so it reads over whatever it
/// overhangs, lit while hovered, or the skin's <see cref="SkinPart.CloseButton"/> art. The caller places it, as a rule
/// absolutely in the corner of what it closes, overhanging its content by 10.</summary>
public sealed class CloseButton : Component<CloseButtonProps>
{
    protected override void Render(Ui ui, in CloseButtonProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(p.Size ?? ComponentSize.Md);
        ui.CanRun(p.OnClick);
        var hovered = ui.IsHovered;
        var backdrop = t.Color(ColorToken.BgBackdrop);
        var disc = ui.Animate(hovered ? t.Color(ColorToken.StateHover).Over(backdrop) : backdrop);
        var glyph = ui.Animate(t.Color(hovered ? ColorToken.FgHover : ColorToken.FgMuted));
        var art = t.Skin(SkinPart.CloseButton);
        var round = new CornerRadius(m.Height / 2f);
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: m.Height,
                height: m.Height,
                background: art is null ? disc : default(Paint),
                border: art is null ? new Border(1f, t.Color(ColorToken.BorderOverlay)) : default,
                radius: round,
                opacity: ui.IsDisabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: Callback.Of(ui.Self(p), static h => h.Props.OnClick.Invoke())
            )
        )
        {
            if (art is { } rest)
            {
                ui.Art(art: rest);
                if (hovered && t.Skin(SkinPart.CloseButtonHover) is { } lit)
                    ui.Art(art: lit);
            }
            else
                ui.Icon(glyph: IconGlyph.Of(IconRole.X), size: m.IconSize, color: glyph);
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: round);
        }
    }
}
