using System.Numerics;

namespace Tralys.Ui;

public enum PointerPhase : byte
{
    Down,
    Move,
    Up,
    Cancel,

    /// <summary>Each update, to the deepest node under a resting mouse.</summary>
    Still,
}

/// <summary>A pointer event as it bubbles: <see cref="Position"/> and <see cref="Size"/> are the current handler
/// node's.</summary>
[FramePath]
public ref struct PointerEvent
{
    internal Node? Handler;
    internal Node? CaptureRequest;
    internal bool ReleaseRequest;
    internal bool Stopped;
    internal bool Prevented;

    internal PointerEvent(
        PointerPhase phase,
        Pointer pointer,
        MouseButton button,
        ModifierKeys modifiers,
        TimeSpan time,
        Vector2 viewPosition,
        int clickCount
    )
    {
        Phase = phase;
        Pointer = pointer;
        Button = button;
        Modifiers = modifiers;
        Time = time;
        ViewPosition = viewPosition;
        ClickCount = clickCount;
    }

    public PointerPhase Phase { get; }

    public Pointer Pointer { get; }

    public MouseButton Button { get; }

    public ModifierKeys Modifiers { get; }

    public TimeSpan Time { get; }

    /// <summary>The buttons held now; a touch or pen in contact holds <see cref="MouseButtons.Left"/>.</summary>
    public MouseButtons Buttons { get; internal set; }

    /// <summary>Whether a touch or pen touches, or a mouse holds a button.</summary>
    public bool IsInContact { get; internal set; }

    /// <summary>Where the pointer is in the current handler node's border-box space.</summary>
    public Vector2 Position { get; internal set; }

    /// <summary>The current handler node's border-box size.</summary>
    public Size Size { get; internal set; }

    /// <summary>Where the pointer is in the view, in layout units.</summary>
    public Vector2 ViewPosition { get; }

    public int ClickCount { get; }

    /// <summary>Sends the pointer's later input to the current handler node, then its ancestors.</summary>
    public void Capture() => CaptureRequest = Handler;

    public void ReleaseCapture() => ReleaseRequest = true;

    /// <summary>Marks the input taken and stops it bubbling.</summary>
    public void StopPropagation() => Stopped = true;

    /// <summary>Marks the input taken and skips what it would do by default: focus, press and click.</summary>
    public void PreventDefault() => Prevented = true;
}

/// <summary>A key going down or up, as it bubbles from the focused node.</summary>
[FramePath]
public ref struct KeyEvent
{
    internal bool Stopped;
    internal bool Prevented;
    internal bool Typed;
    internal bool FromGamepad;

    internal KeyEvent(Key key, ModifierKeys modifiers, bool isRepeat)
    {
        Key = key;
        Modifiers = modifiers;
        IsRepeat = isRepeat;
    }

    public Key Key { get; }

    public ModifierKeys Modifiers { get; }

    public bool IsRepeat { get; }

    /// <summary>Whether this is <paramref name="chord"/>'s key with exactly its modifiers.</summary>
    public readonly bool Is(Chord chord) =>
        chord.Key != Key.None && chord.Key == Key && chord.Modifiers == Modifiers;

    public void StopPropagation() => Stopped = true;

    /// <summary>Marks the key taken and skips its default action.</summary>
    public void PreventDefault() => Prevented = true;
}
