namespace Tralys.Ui.Rendering;

/// <summary>Rasterizes gradients into rows of one premultiplied RGBA8 ramp texture, shared by the draw lists built
/// on this atlas.</summary>
/// <remarks>Call <see cref="BeginFrame"/> once per engine frame, before any view is flattened: a row is only
/// reused once no draw list of the current frame refers to it.</remarks>
public sealed class GradientRampAtlas
{
    public const int Width = 256;
    public const int DefaultRows = 128;

    readonly byte[] _pixels;
    readonly GradientStops?[] _owners;
    readonly int[] _versions;
    readonly long[] _lastUsed;
    readonly Dictionary<GradientStops, int> _rows;
    long _frame;
    int _dirtyFirst = int.MaxValue;
    int _dirtyLast = -1;

    public GradientRampAtlas(int rows = DefaultRows)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rows);
        Rows = rows;
        _pixels = new byte[Width * rows * 4];
        _owners = new GradientStops?[rows];
        _versions = new int[rows];
        _lastUsed = new long[rows];
        Array.Fill(_lastUsed, long.MinValue);
        _rows = new Dictionary<GradientStops, int>(rows, ReferenceEqualityComparer.Instance);
    }

    public int Rows { get; }

    /// <summary>The whole texture, <see cref="Width"/> texels per row, four bytes per texel.</summary>
    public ReadOnlySpan<byte> Pixels => _pixels;

    public void BeginFrame() => _frame++;

    /// <summary>The row holding <paramref name="stops"/>, rasterized into a free row on first use and again whenever
    /// their <see cref="GradientStops.Version"/> moves on; -1 when every row is already in use this frame.</summary>
    public int Acquire(GradientStops stops)
    {
        if (_rows.TryGetValue(stops, out var row))
        {
            _lastUsed[row] = _frame;
            if (_versions[row] != stops.Version)
            {
                _versions[row] = stops.Version;
                Rasterize(stops, row);
            }
            return row;
        }

        row = ReusableRow();
        if (row < 0)
            return -1;

        if (_owners[row] is { } previous)
            _rows.Remove(previous);
        _owners[row] = stops;
        _versions[row] = stops.Version;
        _rows[stops] = row;
        _lastUsed[row] = _frame;
        Rasterize(stops, row);
        return row;
    }

    internal bool TryRetain(GradientStops stops, int row)
    {
        if (!ReferenceEquals(_owners[row], stops) || _versions[row] != stops.Version)
            return false;

        _lastUsed[row] = _frame;
        return true;
    }

    /// <summary>Hands over the rows rasterized since the last call, for upload.</summary>
    public bool TryTakeDirtyRows(out int firstRow, out int rowCount)
    {
        if (_dirtyLast < 0)
        {
            firstRow = 0;
            rowCount = 0;
            return false;
        }

        firstRow = _dirtyFirst;
        rowCount = _dirtyLast - _dirtyFirst + 1;
        _dirtyFirst = int.MaxValue;
        _dirtyLast = -1;
        return true;
    }

    int ReusableRow()
    {
        var oldest = -1;
        for (var row = 0; row < Rows; row++)
        {
            if (_owners[row] is null)
                return row;
            if (_lastUsed[row] < _frame && (oldest < 0 || _lastUsed[row] < _lastUsed[oldest]))
                oldest = row;
        }
        return oldest;
    }

    void Rasterize(GradientStops stops, int row)
    {
        var texels = _pixels.AsSpan(row * Width * 4, Width * 4);
        for (var x = 0; x < Width; x++)
        {
            var (r, g, b, a) = stops.Sample(x / (float)(Width - 1));
            texels[x * 4] = (byte)Color.ToByte(r * a);
            texels[x * 4 + 1] = (byte)Color.ToByte(g * a);
            texels[x * 4 + 2] = (byte)Color.ToByte(b * a);
            texels[x * 4 + 3] = (byte)Color.ToByte(a);
        }

        _dirtyFirst = Math.Min(_dirtyFirst, row);
        _dirtyLast = Math.Max(_dirtyLast, row);
    }
}
