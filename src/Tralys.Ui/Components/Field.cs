using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct FieldProps
{
    public TextValue Label { get; init; }

    /// <summary>Text shown where the control would be, for a field that shows a value rather than editing one; format
    /// it with <c>ui.Format</c>.</summary>
    public TextValue Value { get; init; }

    /// <summary>Shown in the placeholder colour while <see cref="Value"/> is empty.</summary>
    public TextValue Placeholder { get; init; }

    /// <summary>A line under the control: a hint.</summary>
    public TextValue Description { get; init; }

    /// <summary>What is wrong, shown in the danger colour in place of <see cref="Description"/>.</summary>
    public TextValue Error { get; init; }

    /// <summary>Marks the label with an asterisk in the danger colour.</summary>
    public bool Required { get; init; }

    /// <summary>Shows an info icon after the label, and this as the label's tooltip.</summary>
    public TextValue InfoTip { get; init; }

    /// <summary>What the label's tooltip shows when it has no <see cref="InfoTip"/>, such as a breakdown of the value,
    /// behind the same info icon.</summary>
    public Slot InfoTipContent { get; init; }

    /// <summary>Beside its control, the label first; or above it.</summary>
    public Orientation Orientation { get; init; }

    /// <summary>The label column's width beside the control, which fields in one form share to line up; 0 sizes it to
    /// the label.</summary>
    public float LabelWidth { get; init; }

    public bool Disabled { get; init; }
}

/// <summary>A labelled field: a label beside or above its control, written as its children, or a
/// <see cref="FieldProps.Value"/> it shows, and a line beneath for a hint or what is wrong.</summary>
[Children]
public sealed class Field : Component<FieldProps>
{
    static readonly GridTracks Stacked = new(Track.Fr(1f));

    protected override void Render(Ui ui, in FieldProps p)
    {
        var t = ui.Theme;
        var disabled = ui.IsDisabled;
        var vertical = p.Orientation == Orientation.Vertical;
        var weight = t.Weight(WeightToken.Interface);
        var muted = t.Color(disabled ? ColorToken.FgDisabled : ColorToken.FgMuted);
        var column = vertical ? 1 : 2;
        var labelled = !p.Label.Span.IsEmpty;
        var error = !p.Error.Span.IsEmpty;
        var line = error ? p.Error : p.Description;
        using (
            ui.Grid(
                columns: vertical
                    ? Stacked
                    : new GridTracks(p.LabelWidth > 0f ? p.LabelWidth : Track.Auto, Track.Fr(1f)),
                align: Align.Center
            )
        )
        {
            if (labelled)
                using (
                    ui.Box(
                        align: Align.Center,
                        gap: 4f,
                        column: 1,
                        row: 1,
                        margin: vertical
                            ? new Thickness(0f, 0f, 0f, 8f)
                            : new Thickness(0f, 0f, 8f, 0f)
                    )
                )
                {
                    ui.Text(p.Label, color: muted, fontWeight: weight);
                    if (p.Required)
                        ui.Text(
                            "*",
                            color: t.Color(disabled ? ColorToken.FgDisabled : ColorToken.FgDanger),
                            fontWeight: weight
                        );
                    var worded = !p.InfoTip.Span.IsEmpty;
                    if (worded || !p.InfoTipContent.IsEmpty)
                    {
                        ui.Icon(variant: IconVariant.Info);
                        using (
                            ui.Tooltip(
                                text: worded ? p.InfoTip : default,
                                content: p.InfoTipContent,
                                position: Position.Absolute,
                                left: 0f,
                                top: 0f,
                                right: 0f,
                                bottom: 0f
                            )
                        ) { }
                    }
                }

            var shown = !p.Value.Span.IsEmpty;
            if (shown || !p.Placeholder.Span.IsEmpty)
                ui.Text(
                    shown ? p.Value : p.Placeholder,
                    color: t.Color(
                        disabled ? ColorToken.FgDisabled
                        : shown ? ColorToken.FgStrong
                        : ColorToken.FgPlaceholder
                    ),
                    fontWeight: weight,
                    wrap: true,
                    column: column,
                    row: vertical ? 2 : 1
                );
            using (ui.Box(direction: Direction.Column, column: column, row: vertical ? 2 : 1))
                ui.Children();

            if (!line.Span.IsEmpty)
                ui.Text(
                    line,
                    color: disabled ? muted
                        : error ? t.Color(ColorToken.FgDanger)
                        : muted,
                    fontWeight: weight,
                    variant: TextVariant.Caption,
                    wrap: true,
                    margin: new Thickness(0f, 4f, 0f, 0f),
                    column: column,
                    row: vertical ? 3 : 2
                );
        }
    }
}
