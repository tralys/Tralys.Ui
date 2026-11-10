using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct NumberInputProps
{
    public double Value { get; init; }

    /// <summary>Reports the number each edit reads, unsettled, and the value the field settles to: as the focus leaves
    /// it, on Enter, on a step or a pad key.</summary>
    public Callback<double> OnValueChange { get; init; }

    public NumberMode Mode { get; init; }

    public double Min { get; init; }

    /// <summary>The largest value it settles to: unbounded by default; one below <see cref="Min"/> counts as
    /// it.</summary>
    public double? Max { get; init; }

    /// <summary>What a step adds: 1 by default, ten times as much with Shift held.</summary>
    public double? Step { get; init; }

    /// <summary>Shows − and + buttons at its ends that step the value.</summary>
    public bool Steppers { get; init; }

    /// <summary>The digits after the point a <see cref="NumberMode.Decimal"/> value shows without a
    /// <see cref="Format"/>: 2 by default.</summary>
    public int? DecimalPlaces { get; init; }

    /// <summary>A .NET numeric format the value shows and reads through, in place of the mode's own.</summary>
    public string? Format { get; init; }

    /// <summary>Shows zero as an empty field: true by default.</summary>
    public bool? ZeroIsEmpty { get; init; }

    public TextValue Placeholder { get; init; }

    /// <summary>The most characters typing and pasting may make its text hold, the separators a whole number groups
    /// with included, under a count as an <see cref="Input"/>'s; 0 for no limit. A field capped by digits takes a
    /// <see cref="Format"/> without grouping, such as <c>"0"</c>; the <see cref="NumberPad"/> keeps to
    /// <see cref="Max"/> alone.</summary>
    public int MaxLength { get; init; }

    /// <summary>How few characters may remain before its count shows; null shows it whenever there is a
    /// <see cref="MaxLength"/>.</summary>
    public int? CountWithin { get; init; }

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

    /// <summary>Runs on Enter with the value the field settled to.</summary>
    public Callback<double> OnSubmit { get; init; }

    /// <summary>Runs with the value the field settles to, as the focus leaves it, on Enter or on a step, when that
    /// differs from its value as the focus arrived or its last commit, so focusing it and leaving it commits
    /// nothing.</summary>
    public Callback<double> OnCommit { get; init; }
}

/// <summary>An <see cref="Input"/> that edits a number, <see cref="NumberInputProps.Value"/>, raising the number soft
/// keyboard. Each edit reports the number the text reads, an emptied field reading as zero: the text reads through
/// <see cref="NumberInputProps.Format"/> with or without the format's own text around the digits, a percent's number
/// as a hundredth. A whole number without a format regroups its thousands as it is typed, the caret keeping its place
/// from the end; other text stands as typed, and so does text that doesn't read as a number yet, reporting nothing.
/// The field settles as the focus leaves, on Enter or on a step: within its range, an integer losing its fraction; it
/// reports the settled value and shows it again, and what doesn't read as a number puts the value back. The value shows
/// through the format, else as a grouped whole number, else to its decimal places, and zero shows empty unless
/// <see cref="NumberInputProps.ZeroIsEmpty"/> is false. Up and Down step it by <see cref="NumberInputProps.Step"/>,
/// ten times as far with Shift, as its − and + buttons do. Under touch density a whole-number field that can't go
/// negative raises no soft keyboard and opens a <see cref="NumberPad"/> beside it while it holds the focus.</summary>
public sealed class NumberInput : Component<NumberInputProps>
{
    protected override void Render(Ui ui, in NumberInputProps p)
    {
        var model = ui.Remember(static () => new NumberInputModel());
        var own = ui.Ref();
        model.Host = ui.Self(p);
        model.Text = p.NodeRef ?? own;
        model.Field = ui.Ref();
        model.Rewrites = ui.State(0);
        var focused = ui.IsFocusWithin;
        ui.Effect(model, focused, static (m, focused, _) => m.FocusMoved(focused));
        var pad =
            ui.Density == Density.Touch
            && !p.ReadOnly
            && p.Mode == NumberMode.Integer
            && p.Min >= 0.0;
        var max = p.Max ?? double.PositiveInfinity;
        ui.Provide(NumberFieldModel.Steppers, new NumberSteppers(p.Value > p.Min, p.Value < max));
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
                maxLength: p.MaxLength,
                countWithin: p.CountWithin,
                kind: pad ? TextInputKind.None : TextInputKind.Number,
                readOnly: p.ReadOnly,
                invalid: p.Invalid,
                variant: p.Variant,
                size: p.Size,
                height: p.Height,
                textAlign: p.TextAlign,
                leading: p.Steppers ? model.Down : default,
                trailing: p.Steppers ? model.Up : default,
                autoFocus: p.AutoFocus,
                next: p.Next,
                nodeRef: model.Text
            );
        if (pad && focused)
            using (ui.Positioner(anchor: model.Field, placement: PlacementMode.Right))
                ui.NumberPad(
                    hasStep: model.PadStep > 0,
                    hasMax: model.PadMax < long.MaxValue,
                    onKey: model.PadKey
                );
    }
}

[FramePath]
internal sealed class NumberInputModel : NumberFieldModel
{
    internal readonly Slot Down;
    internal readonly Slot Up;
    readonly Callback _stepDown;
    readonly Callback _stepUp;
    readonly Handler<PointerEvent> _press;
    internal Host<NumberInputProps> Host;
    double? _committed;
    bool _shift;
    double _settled;
    double _echo;

    public NumberInputModel()
    {
        _stepDown = Callback.Of(this, static m => m.Clicked(-1));
        _stepUp = Callback.Of(this, static m => m.Clicked(1));
        _press = Handler<PointerEvent>.Of(
            this,
            static (m, ref e) => m._shift = (e.Modifiers & ModifierKeys.Shift) != 0
        );
        Down = Slot.Of(
            this,
            static (ui, m) =>
                ui.NumberStepper(
                    up: false,
                    disabled: !ui.Use(NumberFieldModel.Steppers).CanDown,
                    onStep: m._stepDown,
                    onPress: m._press,
                    margin: new Thickness(4f, 0f, 0f, 0f)
                )
        );
        Up = Slot.Of(
            this,
            static (ui, m) =>
                ui.NumberStepper(
                    up: true,
                    disabled: !ui.Use(NumberFieldModel.Steppers).CanUp,
                    onStep: m._stepUp,
                    onPress: m._press,
                    margin: new Thickness(0f, 0f, 4f, 0f)
                )
        );
    }

    protected override bool ReadOnly => Host.Props.ReadOnly;

    double Max => Host.Props.Max ?? double.PositiveInfinity;

    protected override long PadValue => (long)Host.Props.Value;

    internal override long PadMax => Max < long.MaxValue ? (long)Math.Max(0.0, Max) : long.MaxValue;

    internal override long PadStep => Math.Max(1, (long)(Host.Props.Step ?? 1.0));

    protected override bool Echoed => Host.Props.Value == _echo;

    protected override TextValue Show(Ui ui)
    {
        ref readonly var p = ref Host.Props;
        var value = p.Value;
        if (value == 0.0 && (p.ZeroIsEmpty ?? true))
            return Empty();
        if (p.Format is { } format)
            return Write(ui, value, format);
        if (p.Mode == NumberMode.Integer)
            return Write(ui, value, NumberText.Grouped);
        Span<char> spec = stackalloc char[3];
        spec[0] = 'N';
        Math.Clamp(p.DecimalPlaces ?? 2, 0, 99)
            .TryFormat(
                spec[1..],
                out var digits,
                default,
                System.Globalization.CultureInfo.InvariantCulture
            );
        return Write(ui, value, spec[..(digits + 1)]);
    }

    protected override void ReportPad(long value)
    {
        if (value != (long)Host.Props.Value)
            Host.Props.OnValueChange.Invoke(value);
    }

    protected override void Read(ReadOnlySpan<char> text, int trailing)
    {
        ref readonly var p = ref Host.Props;
        var integer = p.Mode == NumberMode.Integer;
        if (text.IsEmpty)
            Live(0.0);
        else if (NumberText.TryParse(text, integer, p.Format, out var parsed))
        {
            Live(parsed);
            if (integer && p.Format is null)
            {
                EchoGrouped(parsed, text, trailing);
                return;
            }
        }
        else
            return;
        Echo(text);
    }

    void Live(double value)
    {
        ref readonly var p = ref Host.Props;
        _echo = value;
        if (value != p.Value)
            p.OnValueChange.Invoke(value);
    }

    protected override void Settle(ReadOnlySpan<char> text, bool edited)
    {
        ref readonly var p = ref Host.Props;
        var read = p.Value;
        if (edited && text.IsEmpty)
            read = 0.0;
        else if (
            edited
            && NumberText.TryParse(text, p.Mode == NumberMode.Integer, p.Format, out var parsed)
        )
            read = parsed;
        Report(read);
    }

    protected override bool Submit()
    {
        ref readonly var p = ref Host.Props;
        if (p.OnSubmit.IsEmpty)
            return false;
        p.OnSubmit.Invoke(_settled);
        return true;
    }

    protected override bool Step(ReadOnlySpan<char> text, bool edited, int direction, bool tenfold)
    {
        ref readonly var p = ref Host.Props;
        var from = p.Value;
        if (
            edited
            && NumberText.TryParse(text, p.Mode == NumberMode.Integer, p.Format, out var parsed)
        )
            from = parsed;
        var step = p.Step ?? 1.0;
        Report(from + direction * (tenfold ? step * 10.0 : step));
        return true;
    }

    void Clicked(int direction)
    {
        if (ReadOnly)
            return;
        var text = Draft.Span;
        StopEcho();
        Step(text, Edited(text), direction, _shift);
        Take(Index.End);
    }

    void Report(double value)
    {
        ref readonly var p = ref Host.Props;
        var settled =
            p.Mode == NumberMode.Integer
                ? Clamp(Math.Truncate(value), Math.Ceiling(p.Min), Math.Floor(Max))
                : Clamp(value, p.Min, Max);
        _settled = settled;
        if (settled != p.Value)
            p.OnValueChange.Invoke(settled);
        if (settled == (_committed ?? p.Value))
            return;
        if (_committed is not null)
            _committed = settled;
        p.OnCommit.Invoke(settled);
    }

    protected override void FocusArrived() => _committed = Host.Props.Value;

    protected override void FocusLeft() => _committed = null;
}
