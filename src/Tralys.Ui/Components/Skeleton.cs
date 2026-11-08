using Tralys.Ui.Theming;

namespace Tralys.Ui.Components;

public readonly record struct SkeletonProps
{
    public Length Width { get; init; }

    /// <summary>The height; 12 when unset.</summary>
    public Length Height { get; init; }
}

/// <summary>Holds a value's place while it loads, so the layout doesn't move when it lands: a block of the theme's
/// subtle fill, rounded to its <see cref="RadiusToken.Md"/>. Input passes through it.</summary>
public sealed class Skeleton : Component<SkeletonProps>
{
    protected override void Render(Ui ui, in SkeletonProps p)
    {
        var t = ui.Theme;
        using (
            ui.Box(
                width: p.Width,
                height: p.Height.IsAuto ? 12f : p.Height,
                background: t.Color(ColorToken.BgSubtle),
                radius: t.Radius(RadiusToken.Md),
                pointerEvents: PointerEvents.None
            )
        ) { }
    }
}
