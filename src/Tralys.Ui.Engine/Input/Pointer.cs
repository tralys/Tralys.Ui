namespace Tralys.Ui;

public enum PointerKind : byte
{
    Mouse,
    Touch,
    Pen,
}

/// <summary>One pointing device of a view: the mouse, or a finger or pen from the press that put it down until it
/// lifts. Ids need be unique only within their kind.</summary>
public readonly record struct Pointer(PointerKind Kind, ulong Id)
{
    /// <summary>The mouse.</summary>
    public static readonly Pointer Mouse = new(PointerKind.Mouse, 0);
}
