namespace Tralys.Ui.Fonts;

/// <summary>A laid-out glyph: its face and glyph index, and its pen position on the baseline in layout units.</summary>
public readonly struct LayoutGlyph(Font font, int glyph, float x, float y, int run)
{
    public Font Font { get; } = font;

    public int Glyph { get; } = glyph;

    public float X { get; } = x;

    public float Y { get; } = y;

    /// <summary>The buffer run whose style it draws in.</summary>
    public int Run { get; } = run;
}

/// <summary>A laid-out line: the characters it holds (trailing whitespace and its line break included), and where it
/// sits in layout units.</summary>
public readonly struct TextLine(
    int start,
    int length,
    int drawnLength,
    float x,
    float top,
    float baseline,
    float height
)
{
    public int Start { get; } = start;

    public int Length { get; } = length;

    /// <summary>The characters it shows from <see cref="Start"/>: all of <see cref="Length"/>, unless trimming cut the
    /// line at an ellipsis.</summary>
    public int DrawnLength { get; } = drawnLength;

    internal int MeasuredLength { get; init; }

    /// <summary>The left edge of its first glyph, after alignment.</summary>
    public float X { get; } = x;

    public float Top { get; } = top;

    public float Baseline { get; } = baseline;

    public float Height { get; } = height;
}
