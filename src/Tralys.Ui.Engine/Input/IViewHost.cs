namespace Tralys.Ui;

/// <summary>What a <c>UiView</c> asks of the window that hosts it. Tralys.Ui draws no cursor, owns no clipboard,
/// raises no keyboard and plays no haptic itself; the host does each through its platform layer.</summary>
public interface IViewHost
{
    /// <summary>Shows <paramref name="cursor"/> while the mouse is over the view; null when the UI asks for none, so
    /// the host shows its own.</summary>
    [FramePath]
    void SetCursor(Cursor? cursor);

    string? GetClipboardText();

    void SetClipboardText(ReadOnlySpan<char> text);

    /// <summary>Starts text input for a field of <paramref name="kind"/>: places the IME's candidates next to
    /// <paramref name="area"/>, in view pixels, and raises the soft keyboard. <paramref name="byContact"/> says a touch
    /// or pen is what asked: a host may keep the keyboard down otherwise, so that a field focused on load does not cover
    /// the screen. It comes again, with the same kind, when a contact lands on the field already typing.</summary>
    void StartTextInput(TextInputKind kind, Rect area, bool byContact);

    /// <summary>Moves the IME's candidates next to <paramref name="area"/>, in view pixels, as the caret moves while
    /// text input runs; the keyboard stays as it is.</summary>
    void MoveTextInput(Rect area) { }

    void StopTextInput();

    /// <summary>Plays <paramref name="feedback"/> on a device with a haptic motor.</summary>
    void PerformHaptic(HapticFeedback feedback) { }

    /// <summary>Keeps the system's edge gestures, such as a back swipe, off <paramref name="rects"/>, in view pixels,
    /// in place of what the last call kept off.</summary>
    [FramePath]
    void SetSystemGestureExclusions(ReadOnlySpan<Rect> rects) { }
}
