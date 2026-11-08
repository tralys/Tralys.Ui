using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct CardProps
{
    /// <summary>Lights the card, as a grid lights the cell under the pointer or the focus.</summary>
    public bool Highlighted { get; init; }

    /// <summary>Edges the card in the selected colour, highlighted or not.</summary>
    public bool Selected { get; init; }

    public Thickness Padding { get; init; }

    public Length Width { get; init; }

    public Length Height { get; init; }
}

/// <summary>The raised surface of one cell, which stacks what it holds in a column: the theme's card gradient inside a
/// muted 1-unit edge, rounded to the theme's <see cref="RadiusToken.Md"/>. Highlighted, the gradient takes the hover
/// wash and the edge the hover colour; selected, the edge takes the selected colour.</summary>
[Children]
public sealed class Card : Component<CardProps>
{
    protected override void Render(Ui ui, in CardProps p)
    {
        var t = ui.Theme;
        var fills = DisplayFills.Of(t.Theme);
        var edge =
            p.Selected ? ColorToken.BorderSelected
            : p.Highlighted ? ColorToken.BorderHover
            : ColorToken.BorderMuted;
        using (
            ui.Box(
                direction: Direction.Column,
                width: p.Width,
                height: p.Height,
                padding: p.Padding,
                background: p.Highlighted ? fills.CardLit : fills.CardRest,
                border: new Border(1f, t.Color(edge)),
                radius: t.Radius(RadiusToken.Md)
            )
        )
            ui.Children();
    }
}
