using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

/// <summary>Which way a component runs.</summary>
public enum Orientation : byte
{
    Horizontal,
    Vertical,
}

public readonly record struct SeparatorProps
{
    public Orientation Orientation { get; init; }

    /// <summary>The clear on either side of the rule; null is 8.</summary>
    public float? Spacing { get; init; }
}

/// <summary>A 1-unit rule in the theme's border colour between sections, with <see cref="SeparatorProps.Spacing"/>
/// clear on either side: across its line when horizontal, down it when vertical.</summary>
public sealed class Separator : Component<SeparatorProps>
{
    protected override void Render(Ui ui, in SeparatorProps p)
    {
        var line = ui.Theme.Color(ColorToken.Border);
        var clear = p.Spacing ?? 8f;
        if (p.Orientation == Orientation.Vertical)
        {
            using (ui.Box(padding: new Thickness(clear, 0f, clear, 0f)))
            using (ui.Box(width: 1f, background: line)) { }
            return;
        }
        using (ui.Box(direction: Direction.Column, padding: new Thickness(0f, clear, 0f, clear)))
        using (ui.Box(height: 1f, background: line)) { }
    }
}
