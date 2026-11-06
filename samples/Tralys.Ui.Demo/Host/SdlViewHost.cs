namespace Tralys.Ui.Demo;

sealed class SdlViewHost(SdlWindow window) : IViewHost
{
    bool _typing;

    [FramePath]
    public void SetCursor(Cursor? cursor) => window.SetCursor(cursor);

    public string? GetClipboardText() => window.GetClipboardText();

    public void SetClipboardText(ReadOnlySpan<char> text) => window.SetClipboardText(text);

    public void StartTextInput(TextInputKind kind, Rect area, bool byContact)
    {
        window.StartTextInput(kind, area, _typing);
        _typing = true;
    }

    public void MoveTextInput(Rect area) => window.MoveTextInput(area);

    public void StopTextInput()
    {
        if (!_typing)
            return;

        _typing = false;
        window.StopTextInput();
    }
}
