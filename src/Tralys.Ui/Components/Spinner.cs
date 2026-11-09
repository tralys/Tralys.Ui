using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct SpinnerProps
{
    /// <summary>The size whose icon size the spinner stands; <see cref="ComponentSize.Md"/> by default.</summary>
    public ComponentSize? Size { get; init; }
}

/// <summary>The <see cref="IconRole.Loader"/> glyph turning once a second about its own centre, in a square as big as
/// the icon size of its <see cref="SpinnerProps.Size"/>, in the text colour around it. It turns while it shows, and
/// rests, asking for no frames, while hidden or collapsed.</summary>
public sealed class Spinner : Component<SpinnerProps>
{
    protected override void Render(Ui ui, in SpinnerProps p)
    {
        var size = ui.Theme.Metrics(p.Size ?? ComponentSize.Md).IconSize;
        var turn = ui.Loop(1f);
        using (
            ui.Box(
                align: Align.Center,
                justify: Justify.Center,
                width: size,
                height: size,
                transform: Transform.Rotate(turn * 360f)
            )
        )
            ui.Icon(glyph: IconGlyph.Of(IconRole.Loader), size: size, shrink: 0f);
    }
}
