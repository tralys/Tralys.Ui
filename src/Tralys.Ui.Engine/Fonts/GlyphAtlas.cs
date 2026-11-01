using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Fonts;

/// <summary>Where a rasterized glyph sits in a <see cref="GlyphAtlas"/> page: the texel rectangle it draws, and its
/// top-left texel <see cref="Left"/> right of the pen and <see cref="Top"/> above the baseline, in texels at
/// <see cref="GlyphAtlas.EmSize"/>.</summary>
public readonly struct AtlasGlyph(
    RenderTexture page,
    int generation,
    Rect source,
    int left,
    int top
)
{
    public RenderTexture Page { get; } = page;

    /// <summary>The page's generation when the glyph was found; a source rectangle from an older generation no longer
    /// addresses the glyph.</summary>
    public int Generation { get; } = generation;

    public Rect Source { get; } = source;

    public int Left { get; } = left;

    public int Top { get; } = top;
}

/// <summary>Glyphs as signed distance fields of their unhinted outlines, each rasterized once at <see cref="EmSize"/>
/// and scaled to every size it draws at, packed into R8 pages from an <see cref="ITextureFactory"/>. A texel holds 128
/// where its centre lies on the outline, 32 levels more for every texel it lies inside and as many fewer outside,
/// clamped to a byte; a stroked glyph's field is of the band its stroke covers.</summary>
/// <remarks>When no page has room, a new page opens, up to its maximum; past that the least recently used page starts
/// over, unless every page was drawn from since <see cref="BeginFrame"/>, when another opens instead, so a frame never
/// gives up glyphs it has drawn. A page that starts over bumps its texture's <see cref="RenderTexture.Generation"/>, so
/// runs recorded against it draw nothing and their buffers report <see cref="RenderCommandBuffer.IsStale"/> until they
/// record again. It packs into a second texture from then on, trading between the two at each start, so whatever was
/// flattened before it started over still draws what it recorded.</remarks>
[FramePath]
public sealed class GlyphAtlas : IDisposable
{
    // No page's reset drops a glyph with no texels, and a stroke animated through it mints a new key every frame.
    const int MaxBlankKeys = 1024;

    /// <summary>How many pages open, by default, before the least recently used one starts over.</summary>
    public const int DefaultMaxPages = 4;

    /// <summary>The texels per em every glyph rasterizes at, whatever size it draws at.</summary>
    public const float EmSize = 54f;

    /// <summary>How far inside its rasterized field a glyph's drawn rectangle stands, in texels; a cleared texel parts
    /// each glyph from the next, so filtering never reaches a neighbour's.</summary>
    public const int Inset = 0;

    const int Gap = 1;

    readonly ITextureFactory _factory;
    readonly int _pageSize;
    readonly int _maxPages;
    readonly Dictionary<GlyphKey, Entry> _entries = [];
    readonly DistanceFieldRasterizer _field = new();
    byte[] _staged = [];
    ValueList<GlyphKey> _blankKeys;
    int _oldestBlankKey;
    ValueList<Page> _pages = new(DefaultMaxPages);
    long _clock;
    long _frameStart;
    bool _disposed;

    /// <param name="factory">Creates the pages.</param>
    /// <param name="pageSize">Each page's width and height in texels.</param>
    /// <param name="maxPages">How many pages open before the least recently used one starts over, which takes that page
    /// a second texture; a frame that draws more glyphs than they hold opens more.</param>
    public GlyphAtlas(ITextureFactory factory, int pageSize = 2048, int maxPages = DefaultMaxPages)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 16);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 8192);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPages);

        _factory = factory;
        _pageSize = pageSize;
        _maxPages = maxPages;
    }

    /// <summary>Each page's width and height in texels.</summary>
    public int PageSize => _pageSize;

    /// <summary>How many pages are open.</summary>
    public int PageCount => _pages.Count;

    /// <summary>How many times a page has started over.</summary>
    public int ResetCount { get; private set; }

    /// <summary>How many glyphs have been rasterized because the atlas did not hold them.</summary>
    public int MissCount { get; private set; }

    /// <summary>Starts a frame: a page drawn from before it may start over, one drawn from after it may not until the
    /// next. Its <c>UiContext</c> calls it as it advances; until the first call, no page starts over.</summary>
    public void BeginFrame() => _frameStart = _clock;

    /// <summary>Finds <paramref name="glyph"/> of <paramref name="font"/>, stroked <paramref name="strokeWidth"/> wide
    /// with round joins and caps when that is above zero, rasterizing and packing it on first use.
    /// <paramref name="size"/> is the font size it draws at, in the units of <paramref name="strokeWidth"/>, which it
    /// scales the stroke to the em by.</summary>
    /// <returns>False when the glyph draws nothing, such as a space, or is larger than a page.</returns>
    public bool TryGet(Font font, int glyph, float size, float strokeWidth, out AtlasGlyph result)
    {
        ArgumentNullException.ThrowIfNull(font);
        ObjectDisposedException.ThrowIf(_disposed, this);
        result = default;
        if (Font.SizeKey(size) <= 0)
            return false;

        var key = new GlyphKey(
            font.Id,
            glyph,
            strokeWidth > 0f ? Font.SizeKey(strokeWidth * EmSize / size) : 0
        );
        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.Page is null)
                return false;

            entry.Page.LastUse = ++_clock;
            result = entry.Glyph;
            return true;
        }

        MissCount++;
        _field.Clear();
        var tracer = new Tracer(_field, (double)EmSize / font.UnitsPerEm);
        if (
            !font.Face.TryReadOutline(glyph, ref tracer)
            || !_field.TryRasterize(key.Stroke / 128.0, out var bitmap)
            || bitmap.Width > _pageSize
            || bitmap.Height > _pageSize
        )
        {
            RememberBlank(key);
            return false;
        }

        var target = Place(bitmap.Width, bitmap.Height, out var x, out var y);
        Write(target.Texture, x, y, bitmap);
        result = new AtlasGlyph(
            target.Texture,
            target.Texture.Generation,
            new Rect(x + Inset, y + Inset, bitmap.Width - 2 * Inset, bitmap.Height - 2 * Inset),
            bitmap.Left + Inset,
            bitmap.Top - Inset
        );
        _entries.Add(key, new Entry(target, result));
        target.Keys.Add(key);
        target.LastUse = ++_clock;
        return true;
    }

    // A page that started over still holds the glyphs it packed before, so the gap around this one is written cleared.
    void Write(WritableTexture page, int x, int y, in GlyphBitmap bitmap)
    {
        var left = Math.Max(x - Gap, 0);
        var top = Math.Max(y - Gap, 0);
        var width = Math.Min(x + bitmap.Width + Gap, _pageSize) - left;
        var height = Math.Min(y + bitmap.Height + Gap, _pageSize) - top;
        ValueList.EnsureCapacity(ref _staged, width * height);
        var staged = _staged.AsSpan(0, width * height);
        staged.Clear();
        for (var row = 0; row < bitmap.Height; row++)
            bitmap
                .Texels.Slice(row * bitmap.Stride, bitmap.Width)
                .CopyTo(staged.Slice((y - top + row) * width + x - left));
        page.Write(left, top, width, height, staged, width);
    }

    void RememberBlank(GlyphKey key)
    {
        if (_blankKeys.Count < MaxBlankKeys)
            _blankKeys.Add(key);
        else
        {
            _entries.Remove(_blankKeys[_oldestBlankKey]);
            _blankKeys[_oldestBlankKey] = key;
            _oldestBlankKey = (_oldestBlankKey + 1) % MaxBlankKeys;
        }
        _entries.Add(key, default);
    }

    Page Place(int width, int height, out int x, out int y)
    {
        foreach (var open in _pages.AsSpan())
        {
            if (open.TryPack(width, height, out x, out y))
                return open;
        }

        var page = _pages.Count < _maxPages ? null : Evictable();
        if (page is null)
        {
            page = new Page(OpenTexture(), _pageSize);
            _pages.Add(page);
        }
        else
        {
            Reset(page);
        }

        if (!page.TryPack(width, height, out x, out y))
            throw new InvalidOperationException(
                "An empty page had no room for a glyph that fits one."
            );
        return page;
    }

    // Starting over a page this frame drew from leaves its runs stale, to record again next frame and start another over.
    Page? Evictable()
    {
        Page? oldest = null;
        foreach (var page in _pages.AsSpan())
        {
            if (page.LastUse <= _frameStart && (oldest is null || page.LastUse < oldest.LastUse))
                oldest = page;
        }
        return oldest;
    }

    void Reset(Page page)
    {
        page.Texture.BumpGeneration();
        foreach (var key in page.Keys)
            _entries.Remove(key);

        page.StartOver(page.Retired ?? OpenTexture());
        ResetCount++;
    }

    WritableTexture OpenTexture()
    {
        var texture = _factory.CreateWritable(_pageSize, _pageSize);
        if (texture.Width != _pageSize || texture.Height != _pageSize)
        {
            texture.Dispose();
            throw new InvalidOperationException(
                "The texture factory returned a page of another size."
            );
        }
        return texture;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var page in _pages.AsSpan())
        {
            page.Texture.Dispose();
            page.Retired?.Dispose();
        }
        _pages.Clear();
        _entries.Clear();
    }

    readonly record struct GlyphKey(int Font, int Glyph, int Stroke);

    readonly record struct Entry(Page? Page, AtlasGlyph Glyph);

    readonly struct Tracer(DistanceFieldRasterizer field, double scale) : IGlyphOutlineSink
    {
        public void MoveTo(Vector2 point) => field.MoveTo(point.X * scale, point.Y * scale);

        public void LineTo(Vector2 point) => field.LineTo(point.X * scale, point.Y * scale);

        public void QuadraticTo(Vector2 control, Vector2 point) =>
            field.QuadraticTo(
                control.X * scale,
                control.Y * scale,
                point.X * scale,
                point.Y * scale
            );

        public void CubicTo(Vector2 control1, Vector2 control2, Vector2 point) =>
            field.CubicTo(
                control1.X * scale,
                control1.Y * scale,
                control2.X * scale,
                control2.Y * scale,
                point.X * scale,
                point.Y * scale
            );
    }

    sealed class Page(WritableTexture texture, int size)
    {
        ValueList<Shelf> _shelves = new(16);
        int _bottom;

        public WritableTexture Texture { get; private set; } = texture;

        public WritableTexture? Retired { get; private set; }

        public List<GlyphKey> Keys { get; } = [];

        public long LastUse { get; set; }

        public bool TryPack(int width, int height, out int x, out int y)
        {
            x = 0;
            y = 0;
            var shelf = BestShelf(width, height, Math.Max(2, height / 3));
            if (shelf < 0 && _bottom + height > size)
                shelf = BestShelf(width, height, int.MaxValue);

            if (shelf >= 0)
            {
                ref var chosen = ref _shelves[shelf];
                x = chosen.X;
                y = chosen.Y;
                chosen.X += width + Gap;
                return true;
            }

            if (_bottom + height > size)
                return false;

            // Shelves round up to 4 texels, so glyphs a texel or two apart in height share one.
            var shelfHeight = Math.Min((height + Gap + 3) & ~3, size - _bottom);
            _shelves.Add(
                new Shelf
                {
                    Y = _bottom,
                    Height = shelfHeight,
                    X = width + Gap,
                }
            );
            y = _bottom;
            _bottom += shelfHeight;
            return true;
        }

        public void StartOver(WritableTexture next)
        {
            Retired = Texture;
            Texture = next;
            Keys.Clear();
            _shelves.Clear();
            _bottom = 0;
        }

        int BestShelf(int width, int height, int maxWaste)
        {
            var best = -1;
            var bestWaste = int.MaxValue;
            for (var i = 0; i < _shelves.Count; i++)
            {
                ref readonly var shelf = ref _shelves[i];
                var waste = shelf.Height - height - Gap;
                if (waste >= 0 && waste <= maxWaste && waste < bestWaste && shelf.X + width <= size)
                {
                    best = i;
                    bestWaste = waste;
                }
            }
            return best;
        }
    }

    struct Shelf
    {
        public int Y;
        public int Height;
        public int X;
    }
}
