namespace Tralys.Ui;

/// <summary>An edit a focused text field performs, through its key gestures or <c>ui.Command</c>.</summary>
public enum EditCommand : byte
{
    Cut,
    Copy,
    Paste,
    Undo,
    Redo,
    SelectAll,
    Delete,
}
