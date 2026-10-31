namespace Tralys.Ui.Rendering;

/// <summary>An immutable dash pattern, as WPF's <c>StrokeDashArray</c> writes it: the lengths of alternating dashes
/// and gaps, starting with a dash, in stroke thicknesses. An odd count runs twice through to pair every dash with a
/// gap. A pattern whose lengths sum to zero strokes solid.</summary>
public sealed class DashArray
{
    readonly float[] _lengths;

    /// <exception cref="ArgumentOutOfRangeException">A length is negative or not finite.</exception>
    public DashArray(params ReadOnlySpan<float> lengths)
    {
        foreach (var length in lengths)
        {
            if (!(length >= 0f) || !float.IsFinite(length))
                throw new ArgumentOutOfRangeException(
                    nameof(lengths),
                    "Dash and gap lengths must be finite and not negative."
                );
        }

        _lengths = (lengths.Length & 1) == 0 ? lengths.ToArray() : [.. lengths, .. lengths];
        foreach (var length in _lengths)
            Period += length;
    }

    /// <summary>The dash and gap lengths, in stroke thicknesses. The count is always even: an odd count given to the
    /// constructor appears here twice through.</summary>
    public ReadOnlySpan<float> Lengths => _lengths;

    /// <summary>How long the pattern runs before repeating, in stroke thicknesses.</summary>
    public float Period { get; }
}

[FramePath]
internal ref struct DashCursor
{
    public const int MaxSteps = 1 << 16;

    readonly ReadOnlySpan<float> _lengths;
    readonly float _scale;
    int _index;

    public DashCursor(ReadOnlySpan<float> lengths, float scale, float offset)
    {
        _lengths = lengths;
        _scale = scale;
        foreach (var length in lengths)
            Period += length * scale;

        if (!(Period > 0f))
        {
            Remaining = float.PositiveInfinity;
            return;
        }

        var phase = float.IsFinite(offset) ? offset % Period : 0f;
        if (phase < 0f)
            phase += Period;
        for (var skipped = 0; phase > 0f && skipped < lengths.Length * 2; skipped++)
        {
            var length = lengths[_index] * scale;
            if (phase < length)
                break;
            phase -= length;
            _index = (_index + 1) % lengths.Length;
        }
        Remaining = MathF.Max(0f, lengths[_index] * scale - phase);
    }

    public float Period { get; }

    public float Remaining { get; private set; }

    // The lengths come in an even count, so wrapping keeps every dash on an even index.
    public readonly bool On => (_index & 1) == 0;

    public void Step()
    {
        _index = (_index + 1) % _lengths.Length;
        Remaining = _lengths[_index] * _scale;
    }

    public void Advance(float distance) => Remaining -= distance;
}
