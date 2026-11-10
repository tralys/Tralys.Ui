using System.Globalization;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct MoneyInputProps
{
    public long Value { get; init; }

    /// <summary>Reports the amount each edit reads, unsettled, and the amount the field settles to: as the focus leaves
    /// it, on Enter or a pad key.</summary>
    public Callback<long> OnValueChange { get; init; }

    public long Min { get; init; }

    /// <summary>The largest amount it settles to: <see cref="long.MaxValue"/> by default; one below
    /// <see cref="Min"/> counts as it.</summary>
    public long? Max { get; init; }

    public TextValue Placeholder { get; init; }

    public bool ReadOnly { get; init; }

    public bool Invalid { get; init; }

    public FieldVariant Variant { get; init; }

    public ComponentSize? Size { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }

    public TextAlign TextAlign { get; init; }

    public bool Disabled { get; init; }

    public bool AutoFocus { get; init; }

    /// <summary>The field Enter moves the focus to, when it can take it, in place of submitting.</summary>
    public NodeRef? Next { get; init; }

    /// <summary>Bound to its text, the node that takes the focus.</summary>
    public NodeRef? NodeRef { get; init; }

    /// <summary>Runs on Enter with the amount the field settled to.</summary>
    public Callback<long> OnSubmit { get; init; }
}

/// <summary>An <see cref="Input"/> for a whole amount of money, <see cref="MoneyInputProps.Value"/>, grouped by
/// thousands and raising the number soft keyboard. Each edit reports the amount the text reads, a sign dropped and an
/// emptied field reading as zero, and regroups it as it is typed, the caret keeping its place from the end; what
/// doesn't read as a number puts the amount back. As the focus leaves or on Enter the amount settles within its range,
/// is reported and shows again. Zero shows empty. Under touch density it raises no soft keyboard and
/// opens a <see cref="NumberPad"/> with K, M and B beside it while it holds the focus.</summary>
public sealed class MoneyInput : Component<MoneyInputProps>
{
    protected override void Render(Ui ui, in MoneyInputProps p)
    {
        var model = ui.Remember(static () => new MoneyInputModel());
        var own = ui.Ref();
        model.Host = ui.Self(p);
        model.Text = p.NodeRef ?? own;
        model.Field = ui.Ref();
        model.Rewrites = ui.State(0);
        var focused = ui.IsFocusWithin;
        ui.Effect(model, focused, static (m, focused, _) => m.FocusMoved(focused));
        var pad = ui.Density == Density.Touch && !p.ReadOnly;
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                onKeyDown: model.Keys,
                nodeRef: model.Field
            )
        )
            ui.Input(
                text: model.Sync(ui),
                onEdit: model.Edit,
                placeholder: p.Placeholder,
                kind: pad ? TextInputKind.None : TextInputKind.Number,
                readOnly: p.ReadOnly,
                invalid: p.Invalid,
                variant: p.Variant,
                size: p.Size,
                height: p.Height,
                textAlign: p.TextAlign,
                autoFocus: p.AutoFocus,
                next: p.Next,
                nodeRef: model.Text
            );
        if (pad && focused)
            using (ui.Positioner(anchor: model.Field, placement: PlacementMode.Right))
                ui.NumberPad(
                    hasScales: true,
                    hasMax: model.PadMax < long.MaxValue,
                    onKey: model.PadKey
                );
    }
}

[FramePath]
internal sealed class MoneyInputModel : NumberFieldModel
{
    internal Host<MoneyInputProps> Host;
    long _settled;
    long _echo;

    protected override bool ReadOnly => Host.Props.ReadOnly;

    protected override long PadValue => Host.Props.Value;

    internal override long PadMax => Host.Props.Max ?? long.MaxValue;

    internal override long PadStep => 0;

    protected override bool Echoed => Host.Props.Value == _echo;

    protected override TextValue Show(Ui ui)
    {
        var value = Host.Props.Value;
        return value == 0 ? Empty() : Write(ui, value, NumberText.Grouped);
    }

    protected override void Read(ReadOnlySpan<char> text, int trailing)
    {
        ref readonly var p = ref Host.Props;
        var amount = p.Value;
        if (text.IsEmpty)
            amount = 0;
        else if (long.TryParse(text, NumberStyles.Any, Culture, out var parsed))
            amount = Unsigned(parsed);
        _echo = amount;
        if (amount != p.Value)
            p.OnValueChange.Invoke(amount);
        EchoGrouped(amount, text, trailing, blank: amount == 0);
    }

    static long Unsigned(long amount) => amount == long.MinValue ? long.MaxValue : Math.Abs(amount);

    protected override void ReportPad(long value)
    {
        if (value != Host.Props.Value)
            Host.Props.OnValueChange.Invoke(value);
    }

    protected override void Settle(ReadOnlySpan<char> text, bool edited)
    {
        ref readonly var p = ref Host.Props;
        var amount = p.Value;
        if (edited && text.IsEmpty)
            amount = 0;
        else if (edited && long.TryParse(text, NumberStyles.Any, Culture, out var parsed))
            amount = Unsigned(parsed);
        var settled = Clamp(amount, p.Min, p.Max ?? long.MaxValue);
        _settled = settled;
        if (settled != p.Value)
            p.OnValueChange.Invoke(settled);
    }

    protected override bool Submit()
    {
        ref readonly var p = ref Host.Props;
        if (p.OnSubmit.IsEmpty)
            return false;
        p.OnSubmit.Invoke(_settled);
        return true;
    }
}
