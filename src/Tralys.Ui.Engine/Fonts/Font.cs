namespace Tralys.Ui.Fonts;

/// <summary>A face the text engine lays out with, opened by a <see cref="FontCollection"/>: the backend face, its pair
/// kerning, and its advances cached. Sizes are pixels per em; every size scales the face's font units alike.</summary>
[FramePath]
public sealed class Font
{
    const int MaxCachedAdvances = 8192;

    static int s_nextId;

    readonly Dictionary<int, float> _advances = [];
    readonly KerningTable _kerning;
    readonly FaceMetrics _metrics;

    internal Font(FontCollection collection, IFontFace face)
    {
        Collection = collection;
        Face = face;
        Id = Interlocked.Increment(ref s_nextId);

        var unitsPerEm = face.UnitsPerEm;
        UnitsPerEm = unitsPerEm is >= 16 and <= 16384 ? unitsPerEm : 1000;
        _metrics = face.Metrics;
        _kerning = KerningTable.Load(face);
    }

    public FontCollection Collection { get; }

    public IFontFace Face { get; }

    public string FamilyName => Face.FamilyName;

    public int Weight => Face.Weight;

    public bool IsItalic => Face.IsItalic;

    internal int Id { get; }

    internal int UnitsPerEm { get; }

    /// <summary>The face's line metrics at <paramref name="pixelSize"/>, with half the face's own line gap above the
    /// ascent and the rest below the descent, which brings the line to a whole pixel; none at a size too small to set,
    /// below 1/128 pixel.</summary>
    public FaceMetrics GetMetrics(float pixelSize)
    {
        var size = SizeKey(pixelSize);
        if (size <= 0)
            return default;

        var scale = size / 64f / UnitsPerEm;
        var gapAbove = MathF.Max(0f, _metrics.LineGap) * scale / 2f;
        var ascent = _metrics.Ascent * scale + gapAbove;
        var descent = _metrics.Descent * scale;
        var line = (_metrics.Ascent + _metrics.Descent + _metrics.LineGap) * scale;
        return new FaceMetrics(
            ascent,
            descent,
            MathF.Max(0f, MathF.Ceiling(line) - ascent - descent),
            _metrics.UnderlinePosition * scale,
            _metrics.UnderlineThickness * scale
        );
    }

    /// <summary>The fractional advance of <paramref name="glyph"/>, before kerning; none at a size too small to set, as
    /// for <see cref="GetMetrics"/>.</summary>
    public float GetAdvance(int glyph, float pixelSize)
    {
        var size = SizeKey(pixelSize);
        if (size <= 0)
            return 0f;
        if (!_advances.TryGetValue(glyph, out var units))
        {
            if (_advances.Count == MaxCachedAdvances)
                _advances.Clear();

            units = Face.GetAdvance(glyph);
            _advances.Add(glyph, units);
        }
        return (float)(units * (size / 64.0) / UnitsPerEm);
    }

    /// <summary>What kerning adds to the advance of <paramref name="left"/> when <paramref name="right"/> follows it;
    /// negative pulls the pair together.</summary>
    public float GetKerning(int left, int right, float pixelSize)
    {
        if (_kerning.IsEmpty)
            return 0f;

        var units = _kerning.GetUnits(left, right);
        return units == 0 ? 0f : units * (SizeKey(pixelSize) / 64f) / UnitsPerEm;
    }

    internal static int SizeKey(float pixelSize) => (int)MathF.Round(pixelSize * 64f);
}
