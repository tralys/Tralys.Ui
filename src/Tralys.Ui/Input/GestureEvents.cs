using System.Numerics;

namespace Tralys.Ui;

public enum DragPhase : byte
{
    Start,
    Move,
    End,
    Cancel,
}

/// <summary>A press dragged past <see cref="DragThreshold"/>, told to the node with <c>onDrag</c>, whose border-box
/// space <see cref="Position"/> and <see cref="Size"/> are in.</summary>
[FramePath]
public ref struct DragEvent
{
    internal bool Prevented;

    internal DragEvent(
        DragPhase phase,
        Pointer pointer,
        ModifierKeys modifiers,
        Vector2 offset,
        Vector2 delta,
        Vector2 position,
        Size size
    )
    {
        Phase = phase;
        Pointer = pointer;
        Modifiers = modifiers;
        Offset = offset;
        Delta = delta;
        Position = position;
        Size = size;
    }

    public DragPhase Phase { get; }

    public Pointer Pointer { get; }

    public ModifierKeys Modifiers { get; }

    /// <summary>How far the pointer is from the press, in layout units.</summary>
    public Vector2 Offset { get; }

    /// <summary>How far the pointer moved since the last event of this drag.</summary>
    public Vector2 Delta { get; }

    public Vector2 Position { get; }

    public Size Size { get; }

    /// <summary>Whether the touch or pen had started a hold before it moved past the threshold; a mouse never holds.
    /// A component that drags only after a hold refuses the rest at <see cref="DragPhase.Start"/>, leaving the move to
    /// a pan.</summary>
    public bool IsHeld { get; internal init; }

    /// <summary>The mouse button that pressed, so a source can refuse a drag by a button it doesn't take; a touch or a
    /// pen presses as <see cref="MouseButton.Left"/>.</summary>
    public MouseButton Button { get; internal init; }

    /// <summary>At <see cref="DragPhase.Start"/>: refuses the drag.</summary>
    public void PreventDefault() => Prevented = true;
}

/// <summary>A gamepad button or stick, as it bubbles from the focused node.</summary>
[FramePath]
public ref struct GamepadEvent
{
    internal bool Stopped;
    internal bool Prevented;

    internal GamepadEvent(GamepadButton button, bool isDown, bool isRepeat)
    {
        Button = button;
        IsDown = isDown;
        IsRepeat = isRepeat;
    }

    internal GamepadEvent(GamepadStick stick, Vector2 value)
    {
        Stick = stick;
        Value = value;
    }

    /// <summary>The button, or null for a stick.</summary>
    public GamepadButton? Button { get; }

    public bool IsDown { get; }

    public bool IsRepeat { get; }

    /// <summary>The stick, or null for a button.</summary>
    public GamepadStick? Stick { get; }

    /// <summary>The stick's position, -1 to 1 on each axis with Y down; every change is told, the centre included.</summary>
    public Vector2 Value { get; }

    public void StopPropagation() => Stopped = true;

    /// <summary>Marks the input taken and skips what it would do by default.</summary>
    public void PreventDefault() => Prevented = true;
}

/// <summary>A drag-and-drop over or onto a target; the target narrows <see cref="Effect"/> within
/// <see cref="Allowed"/>, and <see cref="DragEffect.None"/> refuses.</summary>
[FramePath]
public ref struct DropEvent
{
    DragEffect _effect;

    internal DropEvent(
        object? data,
        DragEffect allowed,
        DragEffect effect,
        Vector2 position,
        ModifierKeys modifiers
    )
    {
        Data = data;
        Allowed = allowed;
        _effect = effect & allowed;
        Position = position;
        Modifiers = modifiers;
    }

    public object? Data { get; }

    public DragEffect Allowed { get; }

    public DragEffect Effect
    {
        readonly get => _effect;
        set => _effect = value & Allowed;
    }

    /// <summary>Where the pointer is in the target's border-box space.</summary>
    public Vector2 Position { get; internal set; }

    public ModifierKeys Modifiers { get; }

    /// <summary>The mouse button carrying the drag; <see cref="MouseButton.Left"/> for a touch or a pen.</summary>
    public MouseButton Button { get; internal init; }
}
