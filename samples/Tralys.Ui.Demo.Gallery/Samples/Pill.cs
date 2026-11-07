using Tralys.Ui.Theming;

namespace Tralys.Ui.Gallery;

public readonly record struct PillProps
{
    /// <summary>Fills it with the muted accent and edges it in the accent, where the hover and focus edges still
    /// win.</summary>
    public bool Accent { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A sample of a component an app builds on the core: a focusable pill whose look and label follow its own
/// states, read from <c>ui</c> as it renders. Hovered, its edge lightens; pressed, it darkens and its dot takes the
/// accent; focused, its edge thickens in the focus colour; disabled, it fades.</summary>
public sealed class Pill : Component<PillProps>
{
    protected override void Render(Ui ui, in PillProps p)
    {
        var t = ui.Theme;
        var hovered = ui.IsHovered;
        var pressed = ui.IsPressed;
        var focused = ui.IsFocused;
        var disabled = ui.IsDisabled;
        var label =
            disabled ? "Disabled"
            : focused ? "Focused"
            : pressed ? "Pressed"
            : hovered ? "Hovered"
            : "Resting";
        var edge =
            focused ? ColorToken.BorderFocus
            : hovered ? ColorToken.BorderHover
            : p.Accent ? ColorToken.BorderAccent
            : ColorToken.Border;
        var fill =
            pressed ? ColorToken.BgSubtle
            : p.Accent ? ColorToken.BgAccentMuted
            : ColorToken.BgRaised;
        using (
            ui.Box(
                align: Align.Center,
                gap: 8,
                width: 160,
                height: 40,
                padding: new Thickness(12, 0, 12, 0),
                radius: 20,
                background: t.Color(fill),
                border: new(focused ? 2 : 1, t.Color(edge)),
                opacity: disabled ? 0.4f : null,
                focusable: true,
                disabled: p.Disabled
            )
        )
        {
            using (
                ui.Box(
                    width: 10,
                    height: 10,
                    radius: 5,
                    background: t.Color(pressed ? ColorToken.FgAccent : ColorToken.FgMuted)
                )
            ) { }
            ui.Text(label);
        }
    }
}
