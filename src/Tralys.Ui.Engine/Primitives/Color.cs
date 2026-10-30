namespace Tralys.Ui;

/// <summary>An sRGB colour with straight (non-premultiplied) alpha.</summary>
public readonly record struct Color(byte A, byte R, byte G, byte B)
{
    /// <summary>Fully transparent black.</summary>
    public static Color Transparent => default;

    /// <summary>An opaque colour.</summary>
    public static Color FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    /// <summary>A colour from its alpha and colour channels.</summary>
    public static Color FromArgb(byte a, byte r, byte g, byte b) => new(a, r, g, b);

    /// <summary>The colour <paramref name="hex"/> writes as markup and design tokens do: a <c>#</c> then
    /// <c>RGB</c>, <c>ARGB</c>, <c>RRGGBB</c> or <c>AARRGGBB</c> hex digits, alpha first and opaque when left
    /// out.</summary>
    /// <exception cref="FormatException"><paramref name="hex"/> is no such colour.</exception>
    public static Color Parse(ReadOnlySpan<char> hex) =>
        TryParse(hex, out var color)
            ? color
            : throw new FormatException($"'{hex}' is not a hex colour.");

    /// <summary>Reads <paramref name="hex"/> as <see cref="Parse"/> does.</summary>
    /// <returns>Whether <paramref name="hex"/> is such a colour; false for anything else, empty text included.</returns>
    public static bool TryParse(ReadOnlySpan<char> hex, out Color color)
    {
        if (!HexColor.TryParse(hex, out var argb))
        {
            color = default;
            return false;
        }

        color = FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return true;
    }

    /// <summary>This colour laid over the opaque <paramref name="below"/>, as a state layer is laid over a surface.</summary>
    public Color Over(Color below)
    {
        var a = A / 255f;
        return FromRgb(
            (byte)MathF.Round(below.R + (R - below.R) * a),
            (byte)MathF.Round(below.G + (G - below.G) * a),
            (byte)MathF.Round(below.B + (B - below.B) * a)
        );
    }

    /// <summary>The colour with its alpha multiplied by <paramref name="opacity"/>.</summary>
    public Color WithOpacity(float opacity) =>
        this with
        {
            A = (byte)Math.Clamp(MathF.Round(A * opacity), 0f, 255f),
        };

    /// <summary>The colour premultiplied by its alpha and <paramref name="opacity"/>, packed as RGBA8 with red in the low byte.</summary>
    public uint ToPremultipliedRgba(float opacity)
    {
        var alpha = A / 255f * Math.Clamp(opacity, 0f, 1f);
        return Pack(R / 255f * alpha, G / 255f * alpha, B / 255f * alpha, alpha);
    }

    internal static uint Pack(float r, float g, float b, float a) =>
        ToByte(r) | (ToByte(g) << 8) | (ToByte(b) << 16) | (ToByte(a) << 24);

    internal static uint ToByte(float channel) =>
        (uint)MathF.Round(Math.Clamp(channel, 0f, 1f) * 255f);
}
