namespace Tralys.Ui.Fonts.Tests;

sealed class TestFonts : IDisposable
{
    static readonly byte[] InterData = Read("Inter-Regular.otf");
    static readonly byte[] CjkData = Read("NotoSansSC-Subset.otf");

    public TestFonts()
    {
        Collection = new FontCollection();
        Inter = Collection.Add(InterData);
        Cjk = Collection.Add(CjkData);
        Collection.AddFallback(Cjk);
    }

    public FontCollection Collection { get; }

    public Font Inter { get; }

    public Font Cjk { get; }

    public static void SetText(Font font, string text, float size, Color fill, TextLayout into)
    {
        var buffer = new TextBuffer(new TextStyle(font, size, fill));
        buffer.SetText(text);
        into.Update(buffer);
    }

    public void Dispose()
    {
        Collection.Dispose();
    }

    static byte[] Read(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", name));
}
