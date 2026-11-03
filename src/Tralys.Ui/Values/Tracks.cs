using System.Runtime.CompilerServices;

namespace Tralys.Ui;

/// <summary>A grid track's sizing function: fixed, a percentage, content-sized, a share of what is left, or a range
/// between two of those.</summary>
[FramePath]
public readonly record struct Track
{
    Track(TrackSize min, TrackSize max)
    {
        Min = min;
        Max = max;
    }

    internal TrackSize Min { get; }

    internal TrackSize Max { get; }

    public static Track Auto => new(new(TrackKind.Auto, 0f), new(TrackKind.Auto, 0f));

    public static Track MinContent =>
        new(new(TrackKind.MinContent, 0f), new(TrackKind.MinContent, 0f));

    public static Track MaxContent =>
        new(new(TrackKind.MaxContent, 0f), new(TrackKind.MaxContent, 0f));

    public static Track Px(float value) =>
        new(new(TrackKind.Px, Finite(value)), new(TrackKind.Px, Finite(value)));

    public static Track Percent(float value) =>
        new(new(TrackKind.Percent, Finite(value)), new(TrackKind.Percent, Finite(value)));

    /// <summary>A share of the space left, by weight; it sizes from its content up.</summary>
    public static Track Fr(float value) =>
        new(new(TrackKind.Auto, 0f), new(TrackKind.Fr, Finite(MathF.Max(0f, value))));

    /// <summary>At least <paramref name="min"/>, and up to <paramref name="max"/>; the minimum may not be a share.</summary>
    public static Track MinMax(Track min, Track max)
    {
        if (min.Max.Kind == TrackKind.Fr)
            throw new ArgumentException("A track's minimum may not be a share (Fr).", nameof(min));
        return new(min.Min, max.Max);
    }

    public static implicit operator Track(float px) => Px(px);

    static float Finite(float value) =>
        float.IsFinite(value)
            ? value
            : throw Faults.OutOfRange(nameof(value), value, "A track size is finite.");
}

internal enum TrackKind : byte
{
    Auto,
    MinContent,
    MaxContent,
    Px,
    Percent,
    Fr,
}

[FramePath]
internal readonly record struct TrackSize(TrackKind Kind, float Value)
{
    public bool IsContent => Kind is TrackKind.Auto or TrackKind.MinContent or TrackKind.MaxContent;
}

/// <summary>A grid's tracks along one axis: up to eight held inline, or more from a static array.</summary>
[FramePath]
public readonly struct GridTracks : IEquatable<GridTracks>
{
    const int InlineCount = 8;

    readonly Inline _inline;
    readonly Track[]? _array;
    readonly int _count;

    public GridTracks(params ReadOnlySpan<Track> tracks)
    {
        if (tracks.Length > InlineCount)
            throw new ArgumentException(
                "A grid holds up to eight tracks inline; pass more through GridTracks.From.",
                nameof(tracks)
            );
        tracks.CopyTo(_inline);
        _count = tracks.Length;
    }

    GridTracks(Track[] tracks)
    {
        _array = tracks;
        _count = tracks.Length;
    }

    /// <summary><paramref name="count"/> copies of <paramref name="track"/>.</summary>
    public static GridTracks Repeat(int count, Track track)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > InlineCount)
        {
            var tracks = new Track[count];
            Array.Fill(tracks, track);
            return new GridTracks(tracks);
        }

        Span<Track> span = stackalloc Track[count];
        span.Fill(track);
        return new GridTracks(span);
    }

    /// <summary>The tracks of <paramref name="tracks"/>, which must not change: keep it in a static readonly field.</summary>
    public static GridTracks From(Track[] tracks)
    {
        ArgumentNullException.ThrowIfNull(tracks);
        return new GridTracks(tracks);
    }

    public int Count => _count;

    internal Track this[int index] => _array is null ? _inline[index] : _array[index];

    public bool Equals(GridTracks other) => Same(other);

    internal bool Same(in GridTracks other)
    {
        if (_count != other._count)
            return false;
        if (_array is not null && ReferenceEquals(_array, other._array))
            return true;
        for (var i = 0; i < _count; i++)
        {
            if (this[i] != other[i])
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is GridTracks other && Equals(other);

    public override int GetHashCode() => _count;

    public static bool operator ==(GridTracks left, GridTracks right) => left.Equals(right);

    public static bool operator !=(GridTracks left, GridTracks right) => !left.Equals(right);

    [InlineArray(InlineCount)]
    struct Inline
    {
        Track _first;
    }
}
