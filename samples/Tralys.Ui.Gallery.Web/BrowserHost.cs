using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace Tralys.Ui.Gallery.Web;

[SupportedOSPlatform("browser")]
sealed partial class BrowserHost : IViewHost
{
    string? _clipboard;
    CursorType _cursor;

    [JSImport("setCursor", "main.js")]
    private static partial void SetPageCursor(string cursor);

    [JSImport("writeClipboard", "main.js")]
    private static partial void WriteClipboard(string text);

    [JSImport("textInput", "main.js")]
    private static partial void SetTextInput(bool on);

    [FramePath]
    public void SetCursor(Cursor? cursor)
    {
        var type = cursor is { Type: not CursorType.Custom and var shown }
            ? shown
            : CursorType.Arrow;
        if (type == _cursor)
            return;
        _cursor = type;
        SetPageCursor(CssCursor(type));
    }

    public string? GetClipboardText() => _clipboard;

    public void SetClipboardText(ReadOnlySpan<char> text)
    {
        _clipboard = text.ToString();
        WriteClipboard(_clipboard);
    }

    public void StartTextInput(TextInputKind kind, Rect area, bool byContact) => SetTextInput(true);

    public void StopTextInput() => SetTextInput(false);

    public void Paste(string text) => _clipboard = text;

    [FramePath]
    static string CssCursor(CursorType type) =>
        type switch
        {
            CursorType.IBeam => "text",
            CursorType.Wait => "wait",
            CursorType.Crosshair => "crosshair",
            CursorType.WaitArrow => "progress",
            CursorType.SizeNwSe => "nwse-resize",
            CursorType.SizeNeSw => "nesw-resize",
            CursorType.SizeWe => "ew-resize",
            CursorType.SizeNs => "ns-resize",
            CursorType.SizeAll or CursorType.ScrollAll => "move",
            CursorType.No => "not-allowed",
            CursorType.Hand => "pointer",
            _ => "default",
        };
}
