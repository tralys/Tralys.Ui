using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct SliderFieldProps
{
    public TextValue Label { get; init; }

    public double Value { get; init; }

    public Callback<double> OnValueChange { get; init; }

    public double Min { get; init; }

    /// <summary>The largest value: 100 by default.</summary>
    public double? Max { get; init; }

    /// <summary>The increment a drag snaps to, an arrow key moves to the next of, and Page Up and Page Down move ten
    /// of: 1 by default.</summary>
    public double? Step { get; init; }

    /// <summary>What the readout counts in, shown after it in the muted colour: <c>%</c>, <c>px</c>.</summary>
    public TextValue Unit { get; init; }

    public NumberMode Mode { get; init; }

    /// <summary>The digits after the point a <see cref="NumberMode.Decimal"/> readout shows without a
    /// <see cref="Format"/>: 2 by default.</summary>
    public int? DecimalPlaces { get; init; }

    public string? Format { get; init; }

    /// <summary>Holds a dragged thumb's value back until it lets go, for a value that rescales the slider under the
    /// pointer, as the interface size does: each step mid-drag would move the track the drag is measured
    /// against.</summary>
    public bool CommitOnRelease { get; init; }

    /// <summary>The label column's width, which fields in one form share to line up; 0 sizes it to the label.</summary>
    public float LabelWidth { get; init; }

    /// <summary>The readout's width, which slider fields in one form share to line up their sliders; at least 44,
    /// and its text's width while 0.</summary>
    public float ReadoutWidth { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A labelled <see cref="Slider"/> with a <see cref="NumberInput"/> readout beside it, both editing
/// <see cref="SliderFieldProps.Value"/>: a press on the track moves the thumb there, dragging snaps to
/// <see cref="SliderFieldProps.Step"/> and the readout follows, and a number typed into the readout settles there and
/// moves the slider. The value keeps the readout's precision;
/// the slider shows it to a float's.</summary>
public sealed class SliderField : Component<SliderFieldProps>
{
    static readonly Thickness ReadoutGap = new(6f, 0f, 0f, 0f);
    static readonly Thickness UnitGap = new(2f, 0f, 0f, 0f);

    protected override void Render(Ui ui, in SliderFieldProps p)
    {
        var t = ui.Theme;
        var model = ui.Remember(static () => new SliderFieldModel());
        model.Host = ui.Self(p);
        var draft = ui.State<float?>(null);
        model.Draft = draft;
        var max = p.Max ?? 100.0;
        var step = (float)(p.Step ?? 1.0);
        using (ui.Field(label: p.Label, labelWidth: p.LabelWidth))
        using (ui.Box(align: Align.Center))
        {
            ui.Slider(
                value: draft.Value ?? (float)p.Value,
                onValueChange: model.Slid,
                min: (float)p.Min,
                max: (float)max,
                step: step > 0f ? step : null,
                pageStep: step > 0f ? step * 10f : null,
                tickFrequency: step,
                snapToTicks: step > 0f,
                moveToPress: true,
                onDragStart: model.Started,
                onDragEnd: model.Ended,
                grow: 1f
            );
            using (
                ui.Box(
                    direction: Direction.Column,
                    width: p.ReadoutWidth > 0f ? p.ReadoutWidth : Length.Auto,
                    minWidth: 44f,
                    margin: ReadoutGap
                )
            )
                ui.NumberInput(
                    value: p.Value,
                    onValueChange: p.OnValueChange,
                    mode: p.Mode,
                    min: p.Min,
                    max: max,
                    decimalPlaces: p.DecimalPlaces,
                    format: p.Format,
                    zeroIsEmpty: false,
                    size: ComponentSize.Xs
                );
            if (!p.Unit.Span.IsEmpty)
                ui.Text(
                    p.Unit,
                    color: t.Color(ui.IsDisabled ? ColorToken.FgDisabled : ColorToken.FgMuted),
                    fontWeight: t.Weight(WeightToken.Interface),
                    margin: UnitGap
                );
        }
    }
}

[FramePath]
internal sealed class SliderFieldModel
{
    internal readonly Callback<float> Slid;
    internal readonly Callback Started;
    internal readonly Callback<bool> Ended;
    internal Host<SliderFieldProps> Host;
    internal State<float?> Draft = null!;
    bool _dragging;

    public SliderFieldModel()
    {
        Slid = Callback<float>.Of(this, static (m, value) => m.Moved(value));
        Started = Callback.Of(this, static m => m._dragging = true);
        Ended = Callback<bool>.Of(this, static (m, cancelled) => m.Let(cancelled));
    }

    void Moved(float value)
    {
        if (_dragging && Host.Props.CommitOnRelease)
            Draft.Value = value;
        else
            Report(value);
    }

    void Let(bool cancelled)
    {
        _dragging = false;
        if (Draft.Peek is { } held && !cancelled)
            Report(held);
        Draft.Value = null;
    }

    void Report(float shown)
    {
        ref readonly var p = ref Host.Props;
        var step = p.Step ?? 1.0;
        double value = shown;
        if (step > 0.0)
            value = p.Min + Math.Round((shown - p.Min) / step) * step;
        if (p.Mode == NumberMode.Integer)
            value = Math.Round(value);
        if (value != p.Value)
            p.OnValueChange.Invoke(value);
    }
}
