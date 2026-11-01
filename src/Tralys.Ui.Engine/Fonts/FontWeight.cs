namespace Tralys.Ui.Fonts;

/// <summary>How heavy a face is, on the OS/2 weight scale; any value from 1 to 999 picks the nearest face the CSS
/// way.</summary>
public enum FontWeight : short
{
    Thin = 100,
    ExtraLight = 200,
    Light = 300,
    Normal = 400,
    Medium = 500,
    SemiBold = 600,
    Bold = 700,
    ExtraBold = 800,
    Black = 900,
}

public enum FontStyle : byte
{
    Normal,

    /// <summary>An italic face, or else an upright one.</summary>
    Italic,

    /// <summary>As <see cref="Italic"/>: a slanted face is never synthesized.</summary>
    Oblique,
}
