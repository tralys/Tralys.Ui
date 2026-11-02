namespace Tralys.Ui;

/// <summary>The modifier keys held during an input event.</summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,

    /// <summary>The Windows, Command or Super key.</summary>
    Gui = 8,
}
