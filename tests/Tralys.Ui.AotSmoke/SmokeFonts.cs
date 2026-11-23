using Tralys.Ui.Fonts;
using Tralys.Ui.Rendering;

namespace Tralys.Ui.AotSmoke;

sealed class SmokeFonts : IDisposable
{
    readonly FontCollection _fonts;

    public SmokeFonts(ITextureFactory textures)
    {
        _fonts = new FontCollection();
        Services = new TextServices(_fonts, _fonts.Add(Embedded("Inter-Regular.otf")), textures);
    }

    public TextServices Services { get; }

    static byte[] Embedded(string name)
    {
        using var stream =
            typeof(SmokeFonts).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"{name} is not embedded.");
        var bytes = new byte[stream.Length];
        stream.ReadExactly(bytes);
        return bytes;
    }

    public void Dispose()
    {
        Services.Dispose();
        _fonts.Dispose();
    }
}

sealed class SmokeHost : IViewHost
{
    public void SetCursor(Cursor? cursor) { }

    public string? GetClipboardText() => null;

    public void SetClipboardText(ReadOnlySpan<char> text) { }

    public void StartTextInput(TextInputKind kind, Rect area, bool byContact) { }

    public void StopTextInput() { }
}
