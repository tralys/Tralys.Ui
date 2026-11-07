namespace Tralys.Ui.Gallery;

/// <summary>Which edge of its content a <see cref="Reveal"/> shows first.</summary>
public enum RevealDirection : byte
{
    /// <summary>The top edge, the reveal growing down.</summary>
    Down,

    /// <summary>The bottom edge, the reveal growing up.</summary>
    Up,
}

public readonly record struct RevealProps
{
    /// <summary>The share of its content's height it shows, 0 to 1.</summary>
    public float Progress { get; init; }

    public RevealDirection Direction { get; init; }
}

/// <summary>A sample of a component an app builds on the core: it shows <see cref="RevealProps.Progress"/> of what it
/// holds, clipping the rest, and stands that tall. It reads its content's laid-out height through a
/// <see cref="NodeRef"/>, so it takes its height an update after the content changes size.</summary>
[Children]
public sealed class Reveal : Component<RevealProps>
{
    protected override void Render(Ui ui, in RevealProps p)
    {
        var content = ui.Ref();
        var height = content.Size.Height * Math.Clamp(p.Progress, 0f, 1f);
        using (
            ui.Box(
                direction: Direction.Column,
                justify: p.Direction == RevealDirection.Up ? Justify.End : Justify.Start,
                height: height,
                overflow: Overflow.Clip
            )
        )
        using (ui.Box(direction: Direction.Column, shrink: 0, nodeRef: content))
            ui.Children();
    }
}
