using Tralys.Ui.Media;
using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct EmptyStateProps
{
    /// <summary>The message; with none, the empty state draws nothing and takes no space.</summary>
    public TextValue Text { get; init; }

    /// <summary>Art drawn above the message, for a list with nothing in it yet rather than one a search emptied.</summary>
    public ImageSource Emblem { get; init; }

    /// <summary>Shown in place of <see cref="Text"/>, without the emblem, while <see cref="SearchMiss"/> holds.</summary>
    public TextValue NoMatchText { get; init; }

    /// <summary>Set by whoever runs the search: the list is empty because nothing matched it.</summary>
    public bool SearchMiss { get; init; }

    /// <summary>Fades out what lies behind the message, for one drawn over placeholder cells.</summary>
    public bool Backdrop { get; init; }
}

/// <summary>The message a list, grid or pane shows when it has nothing to show: its lines centred in the muted text
/// colour, at most the theme's <see cref="ComponentSize.Sm"/> measure wide, under its emblem at 72 × 72 and 75%
/// opacity. Input passes through it to what lies beneath.</summary>
public sealed class EmptyState : Component<EmptyStateProps>
{
    protected override void Render(Ui ui, in EmptyStateProps p)
    {
        var text = p.SearchMiss ? p.NoMatchText : p.Text;
        if (text.Span.IsEmpty)
            return;
        var t = ui.Theme;
        using (
            ui.Box(
                direction: Direction.Column,
                align: Align.Center,
                gap: 12f,
                padding: new Thickness(12f, 8f, 12f, 8f),
                maxWidth: t.Metrics(ComponentSize.Sm).Measure,
                pointerEvents: PointerEvents.None
            )
        )
        {
            if (p.Backdrop)
                using (
                    ui.Box(
                        position: Position.Absolute,
                        left: -40f,
                        top: -24f,
                        right: -40f,
                        bottom: -24f,
                        background: DisplayFills.Of(t.Theme).EmptyBackdrop
                    )
                ) { }
            if (!p.SearchMiss && !p.Emblem.IsEmpty)
                ui.Image(p.Emblem, width: 72f, height: 72f, opacity: 0.75f);
            ui.Text(
                text,
                color: t.Color(ColorToken.FgMuted),
                fontWeight: t.Weight(WeightToken.Interface),
                textAlign: TextAlign.Center,
                wrap: true
            );
        }
    }
}
