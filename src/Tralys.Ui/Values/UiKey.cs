using System.Runtime.CompilerServices;

namespace Tralys.Ui;

/// <summary>What tells siblings from the same call site apart: an integer, a string (ordinal) or a reference. The
/// default is no key; 0 is a key.</summary>
[FramePath]
public readonly struct UiKey : IEquatable<UiKey>
{
    readonly object? _object;
    readonly long _long;
    readonly KeyKind _kind;

    UiKey(long value)
    {
        _long = value;
        _kind = KeyKind.Long;
    }

    UiKey(object value, KeyKind kind)
    {
        _object = value;
        _kind = kind;
    }

    public static implicit operator UiKey(int value) => new(value);

    public static implicit operator UiKey(long value) => new(value);

    /// <summary>A string key, compared ordinally; null is no key.</summary>
    public static implicit operator UiKey(string? value) =>
        value is null ? default : new UiKey(value, KeyKind.String);

    /// <summary>A key by reference identity; null is no key.</summary>
    public static UiKey Of(object? reference) =>
        reference is null ? default : new UiKey(reference, KeyKind.Reference);

    public bool IsKeyed => _kind != KeyKind.None;

    public bool TryGetLong(out long value)
    {
        value = _long;
        return _kind == KeyKind.Long;
    }

    public bool TryGetString(out string? value)
    {
        value = _kind == KeyKind.String ? (string?)_object : null;
        return _kind == KeyKind.String;
    }

    public bool Equals(UiKey other) =>
        _kind == other._kind
        && _kind switch
        {
            KeyKind.Long => _long == other._long,
            KeyKind.String => string.Equals(
                (string?)_object,
                (string?)other._object,
                StringComparison.Ordinal
            ),
            KeyKind.Reference => ReferenceEquals(_object, other._object),
            _ => true,
        };

    public override bool Equals(object? obj) => obj is UiKey other && Equals(other);

    public override int GetHashCode() =>
        _kind switch
        {
            KeyKind.Long => _long.GetHashCode(),
            KeyKind.String when _object is string text => StringComparer.Ordinal.GetHashCode(text),
            KeyKind.Reference => RuntimeHelpers.GetHashCode(_object),
            _ => 0,
        };

    public static bool operator ==(UiKey a, UiKey b) => a.Equals(b);

    public static bool operator !=(UiKey a, UiKey b) => !a.Equals(b);

    public override string ToString() =>
        _kind switch
        {
            KeyKind.Long => _long.ToString(System.Globalization.CultureInfo.InvariantCulture),
            KeyKind.String when _object is string text => text,
            KeyKind.Reference when _object is { } reference => reference.ToString() ?? "",
            _ => "",
        };

    enum KeyKind : byte
    {
        None,
        Long,
        String,
        Reference,
    }
}
