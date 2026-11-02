namespace Tralys.Ui;

/// <summary>The mouse buttons held during a pointer move, bit <c>1 &lt;&lt; (int)</c><see cref="MouseButton"/> for each.
/// A touch or pen in contact holds <see cref="Left"/>.</summary>
[Flags]
public enum MouseButtons
{
    None = 0,
    Left = 1,
    Middle = 2,
    Right = 4,
    Button1 = 8,
    Button2 = 16,
}
