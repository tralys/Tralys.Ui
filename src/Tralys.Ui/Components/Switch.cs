using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>Which side of its label a <see cref="Switch"/>'s track sits on.</summary>
public enum SwitchSide : byte
{
    Left,
    Right,
}

public readonly record struct SwitchProps
{
    public bool Value { get; init; }

    public Callback<bool> OnValueChange { get; init; }

    public TextValue Label { get; init; }

    /// <summary>What the label shows when it has no <see cref="Label"/> text, in the label's colour, such as a caption
    /// under it; a press on it flips the switch.</summary>
    public Slot Content { get; init; }

    /// <summary>Where the track sits; on the right, the label takes the room before it.</summary>
    public SwitchSide Side { get; init; }

    public ComponentSize? Size { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A controlled on/off switch: it shows <see cref="SwitchProps.Value"/>, sliding its knob across its track as
/// the value changes, and reports a press, Space or gamepad South as the flipped value without changing its own;
/// keyboard Enter passes on to its form or dialog. A skin's
/// <see cref="SkinPart.ToggleTrackOff"/> art replaces the vector track, <see cref="SkinPart.ToggleTrackOn"/> art fades
/// in over it while on, and <see cref="SkinPart.ToggleThumb"/> art replaces the knob.</summary>
public sealed class Switch : Component<SwitchProps>
{
    protected override void Render(Ui ui, in SwitchProps p)
    {
        var t = ui.Theme;
        var m = t.Metrics(p.Size ?? ComponentSize.Md);
        var state = p.Value ? CheckState.Checked : CheckState.Unchecked;
        var right = p.Side == SwitchSide.Right;
        var label = ui.Animate(ui.LabelColor(accent: false));
        using (
            ui.Box(
                align: Align.Center,
                gap: ToggleParts.LabelGap,
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                opacity: ui.IsDisabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: Callback.Of(
                    ui.Self(p),
                    static h => h.Props.OnValueChange.Invoke(!h.Props.Value)
                ),
                pressKeys: PressKeys.Space
            )
        )
        {
            if (!right)
                ui.Indicator(shape: ToggleShape.Track, state: state, extent: ToggleParts.Extent(m));
            if (!p.Label.IsNull)
                ui.Text(p.Label, color: label, grow: 1f);
            else if (!p.Content.IsEmpty)
                using (ui.Box(direction: Direction.Column, grow: 1f, color: label))
                    p.Content.Render(ui);
            if (right)
                ui.Indicator(shape: ToggleShape.Track, state: state, extent: ToggleParts.Extent(m));
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: t.Radius(RadiusToken.Md));
        }
    }
}
