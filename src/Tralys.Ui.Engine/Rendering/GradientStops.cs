namespace Tralys.Ui.Rendering;

public readonly record struct ColorStop(float Offset, Color Color);

/// <summary>An offset-ordered set of gradient stops. Renderers cache its ramp by reference, so share one instance per
/// brush; to animate its stops, keep one instance and <see cref="Set"/> them each frame rather than making
/// another.</summary>
public sealed class GradientStops
{
    ColorStop[] _stops;

    /// <summary>Orders <paramref name="stops"/> by offset; stops sharing an offset keep their order.</summary>
    public GradientStops(ReadOnlySpan<ColorStop> stops)
    {
        if (stops.IsEmpty)
            throw new ArgumentException("A gradient needs at least one stop.", nameof(stops));

        _stops = stops.ToArray();
        Order(_stops);
    }

    public ReadOnlySpan<ColorStop> Stops => _stops;

    /// <summary>Counts the changes to the stops since they were made; a cached ramp of an older version is stale.</summary>
    public int Version { get; private set; }

    /// <summary>Replaces the stops in place, ordered as the constructor orders them, and moves <see cref="Version"/> on,
    /// so every frame drawing them draws them anew; stops equal to those held, in the same order, change nothing. Only
    /// a different number of stops allocates.</summary>
    [FramePath]
    public void Set(ReadOnlySpan<ColorStop> stops)
    {
        if (stops.IsEmpty)
            throw new ArgumentException("A gradient needs at least one stop.", nameof(stops));
        if (stops.SequenceEqual(_stops))
            return;

        if (stops.Length == _stops.Length)
            stops.CopyTo(_stops);
        else
            _stops = stops.ToArray();
        Order(_stops);
        Version++;
    }

    // In sRGB as WPF does, but premultiplied so a transparent stop adds no tint.
    internal (float R, float G, float B, float A) SamplePremultiplied(float t)
    {
        if (t <= _stops[0].Offset)
            return Channels(_stops[0].Color);

        for (var i = 1; i < _stops.Length; i++)
        {
            var next = _stops[i];
            if (t > next.Offset)
                continue;

            var previous = _stops[i - 1];
            var span = next.Offset - previous.Offset;
            var f = span > 0f ? (t - previous.Offset) / span : 1f;
            var (r0, g0, b0, a0) = Channels(previous.Color);
            var (r1, g1, b1, a1) = Channels(next.Color);
            return (r0 + (r1 - r0) * f, g0 + (g1 - g0) * f, b0 + (b1 - b0) * f, a0 + (a1 - a0) * f);
        }

        return Channels(_stops[^1].Color);
    }

    [FramePath]
    static void Order(ColorStop[] stops)
    {
        for (var i = 1; i < stops.Length; i++)
        {
            var stop = stops[i];
            var j = i - 1;
            for (; j >= 0 && stops[j].Offset > stop.Offset; j--)
                stops[j + 1] = stops[j];
            stops[j + 1] = stop;
        }
    }

    static (float R, float G, float B, float A) Channels(Color color)
    {
        var a = color.A / 255f;
        return (color.R / 255f * a, color.G / 255f * a, color.B / 255f * a, a);
    }
}
