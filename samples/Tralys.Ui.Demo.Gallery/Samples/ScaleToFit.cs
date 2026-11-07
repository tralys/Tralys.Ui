namespace Tralys.Ui.Gallery;

/// <summary>Which way a <see cref="ScaleToFit"/> may scale its content.</summary>
public enum FitDirection : byte
{
    Both,

    /// <summary>Only up: content larger than the box keeps its size.</summary>
    UpOnly,

    /// <summary>Only down: content smaller than the box keeps its size.</summary>
    DownOnly,
}

public readonly record struct ScaleToFitProps
{
    /// <summary>Null means <see cref="ObjectFit.Contain"/>.</summary>
    public ObjectFit? Fit { get; init; }

    public FitDirection Direction { get; init; }

    public Length Width { get; init; }
    public Length Height { get; init; }
}

/// <summary>A sample of a component an app builds on the core: a custom layout that lays what it holds out at its own
/// size, in a column, then scales it into its box as <see cref="ScaleToFitProps.Fit"/> says, centred on an axis where
/// it fits and from the top left on one it overflows, in the same layout pass. Given room on one axis only, it scales
/// uniformly to that axis; asked for its narrowest, it has no minimum. Input hits through the scale.</summary>
[Children]
public sealed class ScaleToFit : Component<ScaleToFitProps>
{
    protected override void Render(Ui ui, in ScaleToFitProps p)
    {
        using (
            ui.Layout(
                algorithm: ScaleLayout.Instance,
                state: new Scaling(p.Fit ?? ObjectFit.Contain, p.Direction),
                width: p.Width,
                height: p.Height
            )
        )
        using (ui.Box(direction: Direction.Column))
            ui.Children();
    }
}

readonly record struct Scaling(ObjectFit Fit, FitDirection Direction);

[FramePath]
sealed class ScaleLayout : LayoutAlgorithm<Scaling>
{
    public static readonly ScaleLayout Instance = new();

    public override Size Measure(
        ref LayoutContext cx,
        Scaling state,
        AvailableSpace width,
        AvailableSpace height
    )
    {
        var pad = cx.Padding;
        var across = pad.Left + pad.Right;
        var down = pad.Top + pad.Bottom;
        var room = new Size(Room(width, across), Room(height, down));
        var size = default(Size);
        for (var i = 0; i < cx.ChildCount; i++)
        {
            var content = cx.MeasureChild(i, AvailableSpace.MaxContent, AvailableSpace.MaxContent);
            var (x, y) = Scales(state, room, content);
            size = new Size(
                MathF.Max(size.Width, content.Width * x),
                MathF.Max(size.Height, content.Height * y)
            );
        }
        return new Size(
            width.IsMinContent ? across : size.Width + across,
            height.IsMinContent ? down : size.Height + down
        );
    }

    public override void Arrange(ref LayoutContext cx, Scaling state, Size size)
    {
        var pad = cx.Padding;
        var room = new Size(
            MathF.Max(0f, size.Width - pad.Left - pad.Right),
            MathF.Max(0f, size.Height - pad.Top - pad.Bottom)
        );
        for (var i = 0; i < cx.ChildCount; i++)
        {
            var content = cx.MeasureChild(i, AvailableSpace.MaxContent, AvailableSpace.MaxContent);
            var (x, y) = Scales(state, room, content);
            cx.PlaceChild(
                i,
                new Rect(
                    pad.Left + MathF.Max(0f, room.Width - content.Width * x) / 2f,
                    pad.Top + MathF.Max(0f, room.Height - content.Height * y) / 2f,
                    content.Width,
                    content.Height
                ),
                Transform.Scale(x, y)
            );
        }
    }

    static float Room(AvailableSpace space, float padding) =>
        space.IsDefinite ? MathF.Max(0f, space.Value - padding) : float.NaN;

    static (float X, float Y) Scales(Scaling state, Size room, Size content)
    {
        var boundedX = !float.IsNaN(room.Width);
        var boundedY = !float.IsNaN(room.Height);
        if (state.Fit == ObjectFit.None || !(boundedX || boundedY))
            return (1f, 1f);
        var x = content.Width > 0f ? room.Width / content.Width : 0f;
        var y = content.Height > 0f ? room.Height / content.Height : 0f;
        if (!boundedX)
            x = y;
        else if (!boundedY)
            y = x;
        else if (state.Fit is ObjectFit.Contain or ObjectFit.ScaleDown)
            x = y = MathF.Min(x, y);
        else if (state.Fit == ObjectFit.Cover)
            x = y = MathF.Max(x, y);
        if (state.Fit == ObjectFit.ScaleDown || state.Direction == FitDirection.DownOnly)
            (x, y) = (MathF.Min(x, 1f), MathF.Min(y, 1f));
        else if (state.Direction == FitDirection.UpOnly)
            (x, y) = (MathF.Max(x, 1f), MathF.Max(y, 1f));
        return (x, y);
    }
}
