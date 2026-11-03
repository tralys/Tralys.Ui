using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

internal enum AvailKind : byte
{
    Exact,

    AtMost,
    MaxContent,
    MinContent,
}

[FramePath]
internal readonly record struct Avail(AvailKind Kind, float Value)
{
    public static Avail Exact(float value) => new(AvailKind.Exact, MathF.Max(0f, value));

    public static Avail AtMost(float value) => new(AvailKind.AtMost, MathF.Max(0f, value));

    public static Avail MaxContent => new(AvailKind.MaxContent, float.PositiveInfinity);

    public static Avail MinContent => new(AvailKind.MinContent, 0f);

    public bool IsExact => Kind == AvailKind.Exact;

    public bool IsBounded => Kind is AvailKind.Exact or AvailKind.AtMost;
}

public sealed partial class Node
{
    const int CacheSize = 4;

    internal Rect _bounds;
    internal Rect _viewBounds;
    internal Rect Frame;
    internal Matrix3x2 FrameToView = Matrix3x2.Identity;
    internal Matrix3x2 SnappedToView = Matrix3x2.Identity;
    internal Matrix3x2 ArrangedParent;
    internal Transform Placed;
    internal bool Arranged;
    internal Vector2 PctBasis = new(float.NaN, float.NaN);
    internal ResolvedStyle Env;
    internal RenderCommandBuffer? Commands;
    internal int Span = -1;
    internal long SpanList;
    internal Size RecordedSize;
    internal Rect ContentExtent;
    internal bool MeasurePending;
    MeasureCache _cache;

    internal bool QuarterTurned => (Layout.QuarterTurns & 1) != 0;

    internal bool TryCached(Avail width, Avail height, out Size size)
    {
        for (var i = 0; i < _cache.Count; i++)
        {
            ref readonly var entry = ref _cache.Entries[i];
            if (entry.Width == width && entry.Height == height && entry.Basis.Equals(PctBasis))
            {
                size = entry.Size;
                return true;
            }
        }
        size = default;
        return false;
    }

    internal void Cache(Avail width, Avail height, Size size)
    {
        _cache.Evicted |= _cache.Count == CacheSize;
        var slot = _cache.Count < CacheSize ? _cache.Count++ : _cache.Next++ % CacheSize;
        _cache.Entries[slot] = new MeasureEntry(width, height, PctBasis, size);
    }

    internal void ClearCache()
    {
        _cache.Count = 0;
        _cache.Next = 0;
        _cache.Evicted = false;
        _cache.Baselined = false;
    }

    internal void MarkBaselined() => _cache.Baselined = true;

    internal bool MeasuresHeld => _cache.Count > 0 || _cache.Evicted || _cache.Baselined;

    internal MeasureCache TakeCache()
    {
        var taken = _cache;
        ClearCache();
        return taken;
    }

    void ResetLayout()
    {
        _bounds = _viewBounds = Frame = default;
        FrameToView = SnappedToView = Matrix3x2.Identity;
        ArrangedParent = default;
        Placed = default;
        Arranged = false;
        PctBasis = new Vector2(float.NaN, float.NaN);
        Env = default;
        if (Commands is { } commands)
        {
            commands.Release();
            Pool<RenderCommandBuffer>.Return(commands);
            Commands = null;
        }
        Span = -1;
        SpanList = 0;
        RecordedSize = default;
        ContentExtent = default;
        MeasurePending = false;
        ClearCache();
    }

    internal readonly record struct MeasureEntry(
        Avail Width,
        Avail Height,
        Vector2 Basis,
        Size Size
    );

    internal struct MeasureCache
    {
        public Entries4 Entries;
        public int Count;
        public int Next;
        public bool Evicted;
        public bool Baselined;
    }

    [System.Runtime.CompilerServices.InlineArray(CacheSize)]
    internal struct Entries4
    {
        MeasureEntry _first;
    }
}
