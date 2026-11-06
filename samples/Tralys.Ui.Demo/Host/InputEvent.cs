using System.Numerics;

namespace Tralys.Ui.Demo;

enum InputKind : byte
{
    PointerMove,
    PointerDown,
    PointerUp,
    PointerLeave,
    Wheel,
    TouchDown,
    TouchMove,
    TouchUp,
    TouchCancel,
    KeyDown,
    KeyUp,
    Text,
    FocusLost,
    FocusGained,
    GamepadButtonDown,
    GamepadButtonUp,
    GamepadStick,
}

// Positions are framebuffer pixels, as UiView.Resize takes; Text aliases a buffer the next Poll overwrites.
readonly ref struct InputEvent
{
    public InputKind Kind { get; init; }

    public Vector2 Position { get; init; }

    public Vector2 Wheel { get; init; }

    public MouseButton Button { get; init; }

    public byte Clicks { get; init; }

    public ulong Finger { get; init; }

    public Key Key { get; init; }

    public ModifierKeys Modifiers { get; init; }

    public bool Repeat { get; init; }

    public ReadOnlySpan<char> Text { get; init; }

    public GamepadButton PadButton { get; init; }

    public GamepadStick Stick { get; init; }

    public Vector2 StickValue { get; init; }

    public GamepadStyle PadStyle { get; init; }
}
