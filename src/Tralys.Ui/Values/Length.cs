using System.Globalization;

namespace Tralys.Ui;

/// <summary>A size or offset: auto, layout units, or a percentage of the parent's content box. The default is
/// <see cref="Auto"/>; no length is NaN. One made from NaN or an infinity is not finite, and a node given it keeps the
/// value it had.</summary>
[FramePath]
public readonly struct Length : IEquatable<Length>
{
    readonly float _value;
    readonly LengthUnit _unit;

    Length(float value, LengthUnit unit)
    {
        var finite = float.IsFinite(value);
        _value = finite ? value : 0f;
        _unit = finite ? unit : LengthUnit.NotFinite;
    }

    public static Length Auto => default;

    /// <summary>The whole of the parent's content box: 100%.</summary>
    public static Length Full => new(100f, LengthUnit.Percent);

    public static Length Px(float value) => new(value, LengthUnit.Px);

    public static Length Percent(float percent) => new(percent, LengthUnit.Percent);

    public static implicit operator Length(float px) => Px(px);

    public static implicit operator Length(int px) => Px(px);

    public bool IsAuto => _unit == LengthUnit.Auto;

    internal bool IsPx => _unit == LengthUnit.Px;

    internal bool IsPercent => _unit == LengthUnit.Percent;

    internal bool IsFinite => _unit != LengthUnit.NotFinite;

    internal Length NotNegative => _value < 0f ? new Length(0f, _unit) : this;

    internal float Resolve(float basis) =>
        _unit switch
        {
            LengthUnit.Px => _value,
            LengthUnit.Percent => float.IsNaN(basis) ? float.NaN : basis * _value / 100f,
            _ => float.NaN,
        };

    public bool Equals(Length other) => _unit == other._unit && _value.Equals(other._value);

    public override bool Equals(object? obj) => obj is Length other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_unit, _value);

    public static bool operator ==(Length left, Length right) => left.Equals(right);

    public static bool operator !=(Length left, Length right) => !left.Equals(right);

    public override string ToString()
    {
        if (_unit is not (LengthUnit.Px or LengthUnit.Percent))
            return "auto";
        Span<char> text = stackalloc char[32];
        _value.TryFormat(text, out var written, provider: CultureInfo.InvariantCulture);
        if (_unit == LengthUnit.Percent)
            text[written++] = '%';
        return new string(text.Slice(0, written));
    }

    enum LengthUnit : byte
    {
        Auto,
        Px,
        Percent,
        NotFinite,
    }
}
