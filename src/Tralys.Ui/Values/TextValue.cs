using System.Globalization;
using System.Runtime.CompilerServices;

namespace Tralys.Ui;

/// <summary>Text a prop carries: a string, or characters <see cref="Ui.Format"/> wrote into the frame's arena, which
/// stay valid until the update's render ends. Equality is ordinal on content; null differs from empty.</summary>
[FramePath]
public readonly struct TextValue : IEquatable<TextValue>
{
    readonly string? _string;
    readonly char[]? _chars;
    readonly int _start;
    readonly int _length;

    TextValue(string value)
    {
        _string = value;
        _length = value.Length;
    }

    internal TextValue(char[] chars, int start, int length)
    {
        _chars = chars;
        _start = start;
        _length = length;
    }

    public static implicit operator TextValue(string? value) =>
        value is null ? default : new TextValue(value);

    public bool IsNull => _string is null && _chars is null;

    internal bool IsBorrowed => _chars is not null;

    public ReadOnlySpan<char> Span =>
        _chars is not null ? _chars.AsSpan(_start, _length) : _string.AsSpan();

    public bool Equals(TextValue other) => IsNull == other.IsNull && Span.SequenceEqual(other.Span);

    public override bool Equals(object? obj) => obj is TextValue other && Equals(other);

    public override int GetHashCode() => string.GetHashCode(Span, StringComparison.Ordinal);

    public static bool operator ==(TextValue a, TextValue b) => a.Equals(b);

    public static bool operator !=(TextValue a, TextValue b) => !a.Equals(b);

    /// <summary>The text as a string; allocates unless it is one already.</summary>
    public override string ToString() => _string ?? (_chars is null ? "" : new string(Span));
}

[FramePath]
internal struct HeldText
{
    char[]? _chars;

    public T Hold<T>(T value)
    {
        if (typeof(T) != typeof(TextValue))
            return value;
        var text = Unsafe.As<T, TextValue>(ref value);
        if (!text.IsBorrowed)
            return value;
        var chars = text.Span;
        if (_chars is null || _chars.Length < chars.Length)
            _chars = new char[Math.Max(chars.Length, 16)];
        chars.CopyTo(_chars);
        var held = new TextValue(_chars, 0, chars.Length);
        return Unsafe.As<TextValue, T>(ref held);
    }
}

/// <summary>Formats an interpolated string into the frame's arena, in the invariant culture, allocating nothing once
/// the arena has grown to fit a frame's text.</summary>
[InterpolatedStringHandler]
[FramePath]
public ref struct UiFormat
{
    readonly FrameArena _arena;
    readonly int _start;

    public UiFormat(int literalLength, int formattedCount, Ui ui)
    {
        _arena = ui.Builder.View.Arena;
        _start = _arena.Begin(literalLength + formattedCount * 8);
    }

    public readonly void AppendLiteral(string value) => _arena.Append(value);

    public readonly void AppendFormatted(string? value) => _arena.Append(value);

    public readonly void AppendFormatted(ReadOnlySpan<char> value) => _arena.Append(value);

    public readonly void AppendFormatted(TextValue value) => _arena.Append(value.Span);

    public readonly void AppendFormatted<T>(T value)
        where T : ISpanFormattable
    {
        if (!typeof(T).IsEnum || !EnumText<T>.TryAppend(_arena, value, default))
            _arena.AppendFormatted(value, default);
    }

    public readonly void AppendFormatted<T>(T value, string? format)
        where T : ISpanFormattable
    {
        if (!typeof(T).IsEnum || !EnumText<T>.TryAppend(_arena, value, format))
            _arena.AppendFormatted(value, format);
    }

    internal readonly TextValue ToText() => _arena.End(_start);
}

[FramePath]
internal sealed class FrameArena
{
    char[] _chars = new char[256];
    int _length;

    public void Reset() => _length = 0;

    public int Begin(int hint)
    {
        Reserve(hint);
        return _length;
    }

    public TextValue End(int start) => new(_chars, start, _length - start);

    public void Append(ReadOnlySpan<char> value)
    {
        Reserve(value.Length);
        value.CopyTo(_chars.AsSpan(_length));
        _length += value.Length;
    }

    public void AppendFormatted<T>(T value, ReadOnlySpan<char> format)
        where T : ISpanFormattable
    {
        int written;
        while (
            !value.TryFormat(
                _chars.AsSpan(_length),
                out written,
                format,
                CultureInfo.InvariantCulture
            )
        )
            Grow(_chars.Length * 2);
        _length += written;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Reserve(int count)
    {
        if (_length + count > _chars.Length)
            Grow(_length + count);
    }

    void Grow(int needed)
    {
        var grown = new char[Math.Max(needed, _chars.Length * 2)];
        _chars.AsSpan(0, _length).CopyTo(grown);
        _chars = grown;
    }
}
