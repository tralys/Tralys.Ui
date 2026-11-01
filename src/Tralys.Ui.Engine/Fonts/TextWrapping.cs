namespace Tralys.Ui.Fonts;

public enum TextWrapping : byte
{
    NoWrap,

    /// <summary>Lines break at Unicode line break opportunities, and inside a word only when the word alone is wider
    /// than the line.</summary>
    Wrap,
}

public enum TextTrimming : byte
{
    None,

    /// <summary>A line that overflows ends at the last character that fits with an ellipsis after it.</summary>
    CharacterEllipsis,

    /// <summary>As <see cref="CharacterEllipsis"/>, but at the last word that fits.</summary>
    WordEllipsis,
}

public enum TextAlignment : byte
{
    Left,
    Center,
    Right,
}
