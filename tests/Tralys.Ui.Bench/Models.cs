using Tralys.Ui;
using Tralys.Ui.Fonts;
using Tralys.Ui.FreeType;
using Tralys.Ui.Mvvm;
using Tralys.Ui.Rendering;

namespace Bench;

/// <summary>One card of the dense view: a label, a count and a bar <see cref="Fill"/> units long.</summary>
public sealed partial class Cell : ObservableObject
{
    public Cell(int id)
    {
        Label = "Cell " + id;
        Count = id;
        Fill = id % 100;
    }

    [ObservableProperty]
    public partial string Label { get; set; }

    [ObservableProperty]
    public partial long Count { get; set; }

    [ObservableProperty]
    public partial float Fill { get; set; }

    public static Cell[] Many(int count)
    {
        var cells = new Cell[count];
        for (var i = 0; i < cells.Length; i++)
            cells[i] = new Cell(i);
        return cells;
    }
}

/// <summary>One row of the per-row workload: a label, a count, an opacity and a background shade.</summary>
public sealed partial class Line : ObservableObject
{
    public Line(int id)
    {
        Label = "Line " + id;
        Count = id;
        Opacity = 1f;
        Shade = Shade.Even;
    }

    [ObservableProperty]
    public partial string Label { get; set; }

    [ObservableProperty]
    public partial long Count { get; set; }

    [ObservableProperty]
    public partial float Opacity { get; set; }

    [ObservableProperty]
    public partial Shade Shade { get; set; }

    public static Line[] Many(int count)
    {
        var lines = new Line[count];
        for (var i = 0; i < lines.Length; i++)
            lines[i] = new Line(i);
        return lines;
    }
}

/// <summary>Which of two background colours a row shows.</summary>
public enum Shade
{
    Even,
    Odd,
}

/// <summary>An item of the 10k list; it never changes.</summary>
public sealed class Item(int id)
{
    public int Id { get; } = id;

    public string Label { get; } = "Item " + id;

    public static readonly KeyOf<Item> ById = static item => item.Id;

    public static Item[] Many(int count)
    {
        var items = new Item[count];
        for (var i = 0; i < items.Length; i++)
            items[i] = new Item(i);
        return items;
    }
}

/// <summary>Inter on FreeType, with a glyph atlas in memory: each library gets its own.</summary>
sealed class BenchFonts : IDisposable
{
    static readonly byte[] Inter = Load();

    readonly FreeTypeBackend _backend = new();
    readonly FontCollection _fonts;

    public BenchFonts()
    {
        _fonts = new FontCollection(_backend);
        Services = new TextServices(_fonts, _fonts.Add(Inter), new MemoryTextures());
    }

    public TextServices Services { get; }

    public void Dispose()
    {
        Services.Dispose();
        _fonts.Dispose();
        _backend.Dispose();
    }

    static byte[] Load()
    {
        using var stream = typeof(BenchFonts).Assembly.GetManifestResourceStream(
            "Inter-Regular.otf"
        )!;
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    sealed class MemoryTextures : ITextureFactory
    {
        public WritableTexture CreateWritable(int width, int height) =>
            new MemoryTexture(width, height);
    }

    sealed class MemoryTexture(int width, int height) : WritableTexture
    {
        readonly byte[] _texels = new byte[width * height];

        public override int Width => width;

        public override int Height => height;

        public override void Write(
            int x,
            int y,
            int w,
            int h,
            ReadOnlySpan<byte> texels,
            int stride
        )
        {
            for (var row = 0; row < h; row++)
                texels.Slice(row * stride, w).CopyTo(_texels.AsSpan((y + row) * width + x));
        }

        public override void Dispose() { }
    }
}

sealed class BenchHost : IViewHost
{
    public void SetCursor(Cursor? cursor) { }

    public string? GetClipboardText() => null;

    public void SetClipboardText(ReadOnlySpan<char> text) { }

    public void StartTextInput(TextInputKind kind, Rect area, bool byContact) { }

    public void StopTextInput() { }
}
