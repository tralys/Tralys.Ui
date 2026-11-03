using System.Numerics;
using Tralys.Ui.Rendering;

namespace Tralys.Ui;

public enum Direction : byte
{
    Row,
    Column,
}

public enum FlexWrap : byte
{
    NoWrap,
    Wrap,
    WrapReverse,
}

/// <summary>Where a container places its items across its main axis, as CSS <c>align-items</c>.</summary>
public enum Align : byte
{
    Stretch,
    Start,
    Center,
    End,
    Baseline,
}

/// <summary>An item's own <see cref="Align"/>, as CSS <c>align-self</c>; <see cref="Auto"/> takes the container's.</summary>
public enum AlignSelf : byte
{
    Auto,
    Stretch,
    Start,
    Center,
    End,
    Baseline,
}

/// <summary>How a container shares free space along its main axis, as CSS <c>justify-content</c>.</summary>
public enum Justify : byte
{
    Start,
    Center,
    End,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

/// <summary>How a wrapping container places its lines, as CSS <c>align-content</c>.</summary>
public enum AlignContent : byte
{
    Stretch,
    Start,
    Center,
    End,
    SpaceBetween,
    SpaceAround,
    SpaceEvenly,
}

public enum Position : byte
{
    Relative,

    /// <summary>Out of the flow, placed against the parent's padding box.</summary>
    Absolute,
}

public enum Overflow : byte
{
    Visible,
    Clip,
    ScrollX,
    ScrollY,
    Scroll,
}

/// <summary>Whether a node takes pointer input; inherited, as in CSS.</summary>
public enum PointerEvents : byte
{
    Inherit,
    Auto,
    None,
}

/// <summary>When a pressable clicks.</summary>
public enum PressMode : byte
{
    /// <summary>On a release over it, the press having captured the pointer.</summary>
    Release,

    /// <summary>As the press lands.</summary>
    Press,

    /// <summary>On any release over it, pressed there or not.</summary>
    ReleaseOver,

    /// <summary>As a mouse press lands, and on a touch's or pen's tap as <see cref="Release"/> does, so a finger can
    /// still pan, hold or slide off it before it acts.</summary>
    PressOrTap,
}

/// <summary>Which keys click a focused pressable.</summary>
public enum PressKeys : byte
{
    /// <summary>Enter on every key-down, repeats included, and Space as it comes up, once a hold; gamepad
    /// <see cref="GamepadButton.South"/> acts as Enter.</summary>
    EnterAndSpace,

    /// <summary>Space and gamepad <see cref="GamepadButton.South"/>: keyboard Enter passes on to shortcuts and actions,
    /// such as a dialog's <see cref="UiAction.Confirm"/>, as a checkbox's or a switch's does.</summary>
    Space,

    /// <summary>No key: only a pointer presses it, and Enter, Space and gamepad <see cref="GamepadButton.South"/> all
    /// pass on, as they do from a tab its focus has already selected.</summary>
    None,
}

public enum TextAlign : byte
{
    Inherit,
    Left,
    Center,
    Right,
}

/// <summary>What moved focus. The D-pad counts as <see cref="Arrow"/>, an autofocus as <see cref="Code"/>.</summary>
public enum FocusCause : byte
{
    Pointer,
    Tab,
    Arrow,
    Code,
}

/// <summary>A node gaining or losing focus, and why.</summary>
[FramePath]
public readonly record struct FocusChange(bool Focused, FocusCause Cause);

/// <summary>What a checkbox that can show neither on nor off shows.</summary>
public enum CheckState : byte
{
    Unchecked,
    Checked,
    Indeterminate,
}

public enum NodeKind : byte
{
    Root,
    Box,
    Grid,
    Text,
    Span,
    EditableText,
    Image,
    Canvas,
    Layout,
    VirtualList,
    Layer,
    Component,
    Fragment,
    ActionScope,
}

[Flags]
public enum InputStates : ushort
{
    None = 0,
    Hovered = 1,
    Pressed = 2,
    Focused = 4,
    FocusVisible = 8,
    FocusWithin = 16,
    Disabled = 32,
    Selected = 64,
    Capturing = 128,
    DropTarget = 256,
}

/// <summary>A box's border: its width on each side, inside the box, and its paint.</summary>
[FramePath]
public readonly record struct Border(Thickness Width, Paint Paint)
{
    public Border(float width, Paint paint)
        : this(new Thickness(width), paint) { }
}

/// <summary>A 2D affine transform applied after layout, to paint and hit testing; the default is the identity.</summary>
[FramePath]
public readonly struct Transform : IEquatable<Transform>
{
    // Stored as the difference from the identity, so the default is the identity.
    readonly Matrix3x2 _offset;

    Transform(Matrix3x2 matrix) => _offset = matrix - Matrix3x2.Identity;

    public static Transform Translate(float x, float y) => new(Matrix3x2.CreateTranslation(x, y));

    /// <summary>A clockwise rotation, in degrees.</summary>
    public static Transform Rotate(float degrees) =>
        new(Matrix3x2.CreateRotation(degrees * (MathF.PI / 180f)));

    public static Transform Scale(float s) => new(Matrix3x2.CreateScale(s));

    public static Transform Scale(float x, float y) => new(Matrix3x2.CreateScale(x, y));

    /// <summary>A skew by angles in degrees, as CSS <c>skew(x, y)</c>: each point moves along x by its y times the
    /// tangent of <paramref name="x"/>, and along y by its x times the tangent of <paramref name="y"/>.</summary>
    public static Transform Skew(float x, float y) =>
        new(Matrix3x2.CreateSkew(x * (MathF.PI / 180f), y * (MathF.PI / 180f)));

    /// <summary>Any affine transform: CSS <c>matrix(a, b, c, d, e, f)</c> is
    /// <c>new Matrix3x2(a, b, c, d, e, f)</c>.</summary>
    public static Transform FromMatrix(Matrix3x2 matrix) => new(matrix);

    /// <summary>This transform, then <paramref name="next"/>.</summary>
    public Transform Then(in Transform next) => new(Matrix * next.Matrix);

    public Matrix3x2 Matrix => _offset + Matrix3x2.Identity;

    public bool IsIdentity => _offset == default;

    public bool Equals(Transform other) => _offset.Equals(other._offset);

    public override bool Equals(object? obj) => obj is Transform other && Equals(other);

    public override int GetHashCode() => _offset.GetHashCode();

    public static bool operator ==(Transform left, Transform right) => left.Equals(right);

    public static bool operator !=(Transform left, Transform right) => !left.Equals(right);
}
