using System.Numerics;

namespace Tralys.Ui;

/// <summary>How content is scaled to fill the space it is given.</summary>
public enum Stretch : byte
{
    /// <summary>Kept at its own size.</summary>
    None,

    /// <summary>Scaled on each axis to fill the space exactly, changing its aspect ratio.</summary>
    Fill,

    /// <summary>Scaled to fit inside the space, keeping its aspect ratio.</summary>
    Uniform,

    /// <summary>Scaled to cover the space, keeping its aspect ratio; the excess is cut off.</summary>
    UniformToFill,
}

public enum StretchDirection : byte
{
    /// <summary>Only ever scaled up.</summary>
    UpOnly,

    /// <summary>Only ever scaled down.</summary>
    DownOnly,

    Both,
}

static class StretchScale
{
    [FramePath]
    public static Vector2 Of(
        Size available,
        Size content,
        Stretch stretch,
        StretchDirection direction
    )
    {
        var scaleX = 1f;
        var scaleY = 1f;
        var boundedWidth = !float.IsPositiveInfinity(available.Width);
        var boundedHeight = !float.IsPositiveInfinity(available.Height);
        if (stretch == Stretch.None || !(boundedWidth || boundedHeight))
            return new Vector2(scaleX, scaleY);

        scaleX = FloatUtil.IsZero(content.Width) ? 0f : available.Width / content.Width;
        scaleY = FloatUtil.IsZero(content.Height) ? 0f : available.Height / content.Height;
        if (!boundedWidth)
            scaleX = scaleY;
        else if (!boundedHeight)
            scaleY = scaleX;
        else if (stretch == Stretch.Uniform)
            scaleX = scaleY = MathF.Min(scaleX, scaleY);
        else if (stretch == Stretch.UniformToFill)
            scaleX = scaleY = MathF.Max(scaleX, scaleY);

        switch (direction)
        {
            case StretchDirection.UpOnly:
                scaleX = MathF.Max(scaleX, 1f);
                scaleY = MathF.Max(scaleY, 1f);
                break;
            case StretchDirection.DownOnly:
                scaleX = MathF.Min(scaleX, 1f);
                scaleY = MathF.Min(scaleY, 1f);
                break;
        }
        return new Vector2(scaleX, scaleY);
    }
}
