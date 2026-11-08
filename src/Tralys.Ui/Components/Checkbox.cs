using System.Runtime.CompilerServices;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct CheckboxProps<TValue>
    where TValue : struct
{
    /// <summary>A <see cref="bool"/>, or a <see cref="CheckState"/> for a box that can show
    /// <see cref="CheckState.Indeterminate"/>.</summary>
    public TValue Value { get; init; }

    public Callback<TValue> OnValueChange { get; init; }

    public TextValue Label { get; init; }

    /// <summary>Shows an info icon after the label, whose tooltip says this.</summary>
    public TextValue InfoTip { get; init; }

    public ComponentSize? Size { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A controlled checkbox: it shows <see cref="CheckboxProps{TValue}.Value"/>, ticking or unticking its box as
/// the value changes, and reports a press, Space or gamepad South as the next value without changing its own: the
/// flipped <see cref="bool"/>, or for a <see cref="CheckState"/> unchecked, checked, then indeterminate in turn. A
/// two-state box also reports true for the plus key and false for the minus key, with or without Shift. Keyboard Enter
/// passes on to its form or dialog. An <see cref="CheckboxProps{TValue}.InfoTip"/> shows an info icon after the label,
/// which shows the tip under a resting pointer.</summary>
/// <typeparam name="TValue"><see cref="bool"/> or <see cref="CheckState"/>; any other faults its render.</typeparam>
public sealed partial class Checkbox<TValue> : Component<CheckboxProps<TValue>>
    where TValue : struct
{
    const float InfoGap = 8f;

    protected override void Render(Ui ui, in CheckboxProps<TValue> p)
    {
        var t = ui.Theme;
        var size = p.Size ?? ComponentSize.Md;
        var m = t.Metrics(size);
        var self = ui.Self(p);
        var tipped = !p.InfoTip.Span.IsEmpty;
        var label = ui.Animate(ui.LabelColor(accent: false));
        using (
            ui.Box(
                align: Align.Center,
                gap: ToggleParts.LabelGap,
                fontSize: m.FontSize,
                fontWeight: t.Weight(WeightToken.Interface),
                opacity: ui.IsDisabled ? 0.5f : null,
                cursor: Cursors.Hand,
                onClick: Callback.Of(self, static h => Report(h, Next(StateOf(h.Props.Value)))),
                pressKeys: PressKeys.Space,
                onKeyDown: Handler<KeyEvent>.Of(self, static (h, ref e) => KeyDown(h, ref e))
            )
        )
        {
            ui.Indicator(
                shape: ToggleShape.Box,
                state: StateOf(p.Value),
                extent: ToggleParts.Extent(m)
            );
            if (tipped)
                using (ui.Box(align: Align.Center))
                {
                    if (!p.Label.IsNull)
                        ui.Text(p.Label, color: label);
                    using (ui.Tooltip(text: p.InfoTip, margin: new Thickness(InfoGap, 0f, 0f, 0f)))
                        ui.Icon(variant: IconVariant.Info, rung: size);
                }
            else if (!p.Label.IsNull)
                ui.Text(p.Label, color: label, grow: 1f);
            if (ui.IsFocusVisible)
                ui.FocusRing(radius: t.Radius(RadiusToken.Md));
        }
    }

    [FramePath]
    static CheckState Next(CheckState state) =>
        state switch
        {
            CheckState.Unchecked => CheckState.Checked,
            CheckState.Checked when typeof(TValue) == typeof(CheckState) =>
                CheckState.Indeterminate,
            _ => CheckState.Unchecked,
        };

    [FramePath]
    static void KeyDown(Host<CheckboxProps<TValue>> h, ref KeyEvent e)
    {
        if (typeof(TValue) != typeof(bool) || (e.Modifiers & ~ModifierKeys.Shift) != 0)
            return;
        if (e.Key is Key.OemPlus or Key.Add)
            Report(h, CheckState.Checked);
        else if (e.Key is Key.OemMinus or Key.Subtract)
            Report(h, CheckState.Unchecked);
        else
            return;
        e.PreventDefault();
    }

    [FramePath]
    static void Report(Host<CheckboxProps<TValue>> h, CheckState state)
    {
        ref readonly var p = ref h.Props;
        if (StateOf(p.Value) != state)
            p.OnValueChange.Invoke(ValueOf(state));
    }

    [FramePath]
    static CheckState StateOf(TValue value)
    {
        if (typeof(TValue) == typeof(bool))
            return Unsafe.As<TValue, bool>(ref value) ? CheckState.Checked : CheckState.Unchecked;
        if (typeof(TValue) != typeof(CheckState))
            throw Unsupported();
        return Unsafe.As<TValue, CheckState>(ref value);
    }

    [FramePath]
    static TValue ValueOf(CheckState state)
    {
        if (typeof(TValue) != typeof(bool))
            return Unsafe.As<CheckState, TValue>(ref state);
        var on = state == CheckState.Checked;
        return Unsafe.As<bool, TValue>(ref on);
    }

    static NotSupportedException Unsupported() =>
        new($"A Checkbox holds a bool or a CheckState, not a {typeof(TValue).Name}.");
}
