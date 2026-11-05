using System.Runtime.InteropServices;
using Tralys.Ui.Fonts;
using Tralys.Ui.FreeType;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.Testing;

internal static class TestFonts
{
    static readonly byte[] Inter = Load();

    [ThreadStatic]
    static TextServices? t_services;

    [ThreadStatic]
    static FreeTypeBackend? t_backend;

    [ThreadStatic]
    static FontCollection? t_fonts;

    public static TextServices ForThisThread()
    {
        if (t_services is { } services)
            return services;
        t_backend = new FreeTypeBackend();
        var fonts = t_fonts = new FontCollection(t_backend);
        var face = fonts.Add(Inter);
        return t_services = new TextServices(fonts, face, new MemoryTextures(), atlasPageSize: 512);
    }

    public static void ReleaseThread()
    {
        t_services?.Dispose();
        t_services = null;
        t_fonts?.Dispose();
        t_fonts = null;
        t_backend?.Dispose();
        t_backend = null;
    }

    static byte[] Load()
    {
        using var stream = typeof(TestFonts).Assembly.GetManifestResourceStream(
            "Tralys.Ui.Testing.Inter-Regular.otf"
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

internal sealed class TestHost : IViewHost
{
    public Cursor? Cursor { get; private set; }

    public string? Clipboard { get; set; }

    public TextInputState? TextInput { get; private set; }

    public void SetCursor(Cursor? cursor) => Cursor = cursor;

    public string? GetClipboardText() => Clipboard;

    public void SetClipboardText(ReadOnlySpan<char> text) => Clipboard = text.ToString();

    public void StartTextInput(TextInputKind kind, Rect area, bool byContact) =>
        TextInput = new TextInputState(kind, area, byContact, (TextInput?.Starts ?? 0) + 1);

    public void MoveTextInput(Rect area)
    {
        if (TextInput is { } input)
            TextInput = input with { Area = area };
    }

    public void StopTextInput() => TextInput = null;

    readonly List<Rect> _exclusions = [];

    public ReadOnlySpan<Rect> Exclusions => CollectionsMarshal.AsSpan(_exclusions);

    public int ExclusionsSent { get; private set; }

    public void SetSystemGestureExclusions(ReadOnlySpan<Rect> rects)
    {
        _exclusions.Clear();
        _exclusions.AddRange(rects);
        ExclusionsSent++;
    }
}
