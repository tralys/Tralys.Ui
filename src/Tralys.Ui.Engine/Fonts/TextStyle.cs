using Tralys.Ui.Rendering;

namespace Tralys.Ui.Fonts;

/// <summary>How a run of text draws: its font and its size in layout units per em, its fill, a stroke
/// <see cref="StrokeWidth"/> wide, centred on the outline and drawn beneath the fill, whether its digits are set
/// in one width, and the space it adds after each character.</summary>
public readonly struct TextStyle
{
    public TextStyle(
        Font font,
        float size,
        Paint fill,
        float strokeWidth = 0f,
        Paint stroke = default,
        bool tabularDigits = false,
        int characterSpacing = 0
    )
    {
        ArgumentNullException.ThrowIfNull(font);
        ThrowUnlessPositiveFinite(size, "A size must be positive and finite.", nameof(size));
        if (!(strokeWidth >= 0f) || float.IsPositiveInfinity(strokeWidth))
            throw new ArgumentOutOfRangeException(
                nameof(strokeWidth),
                strokeWidth,
                "A stroke width must be zero or positive, and finite."
            );

        Font = font;
        Size = size;
        Fill = fill;
        StrokeWidth = strokeWidth;
        Stroke = stroke;
        TabularDigits = tabularDigits;
        CharacterSpacing = characterSpacing;
    }

    public Font Font { get; }

    public float Size { get; }

    public Paint Fill { get; }

    public float StrokeWidth { get; }

    public Paint Stroke { get; }

    /// <summary>Whether every digit from 0 to 9 is as wide as the font's widest one, centred in that width.</summary>
    public bool TabularDigits { get; }

    /// <summary>The space added after each character, in thousandths of an em; negative draws characters closer. A
    /// base and the marks on it are one character.</summary>
    public int CharacterSpacing { get; }

    public bool HasStroke => StrokeWidth > 0f && Stroke.IsVisible;

    internal float SpacingAdvance => CharacterSpacing * Size / 1000f;

    internal TextStyle WithFill(Paint fill) =>
        new(Font, Size, fill, StrokeWidth, Stroke, TabularDigits, CharacterSpacing);

    internal bool LaysOutLike(in TextStyle other) =>
        ReferenceEquals(Font, other.Font)
        && Size == other.Size
        && TabularDigits == other.TabularDigits
        && CharacterSpacing == other.CharacterSpacing;

    internal static void ThrowIfDefault(in TextStyle style, string paramName)
    {
        if (style.Font is null)
            throw new ArgumentException("A style needs a font.", paramName);
    }

    internal static void ThrowUnlessPositiveFinite(float value, string message, string paramName)
    {
        if (!(value > 0f) || float.IsPositiveInfinity(value))
            throw OutOfRange(value, message, paramName);
    }

    static ArgumentOutOfRangeException OutOfRange(float value, string message, string paramName) =>
        new(paramName, value, message);
}

/// <summary>The characters <see cref="Start"/> to <see cref="Start"/> + <see cref="Length"/> of a
/// <see cref="TextBuffer"/>, drawn in <see cref="Style"/>.</summary>
public readonly struct TextRun(int start, int length, TextStyle style)
{
    public int Start { get; } = start;

    public int Length { get; } = length;

    public readonly TextStyle Style = style;
}
