namespace Tralys.Ui;

/// <summary>What a text field takes, which picks the soft keyboard a touch device raises for it.</summary>
public enum TextInputKind
{
    Text,

    /// <summary>A name, which no keyboard should correct or suggest.</summary>
    Name,

    Number,

    /// <summary>Hidden as typed, and kept out of the keyboard's suggestions and learning.</summary>
    Password,

    /// <summary>Raises no soft keyboard and takes no text from the host: the field types through keys of its own, as a
    /// number pad.</summary>
    None,
}
