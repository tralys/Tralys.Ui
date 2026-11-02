namespace Tralys.Ui;

/// <summary>The shapes a host draws for a <see cref="Cursor"/>.</summary>
public enum CursorType : byte
{
    Arrow,
    IBeam,
    Wait,
    Crosshair,
    WaitArrow,
    SizeNwSe,
    SizeNeSw,
    SizeWe,
    SizeNs,
    SizeAll,
    No,
    Hand,
    ScrollAll,

    /// <summary>A cursor the host knows by <see cref="Cursor.Name"/>.</summary>
    Custom,
}

/// <summary>The cursor shown while the mouse is over an element; the host maps it to what it draws.</summary>
public sealed class Cursor : IEquatable<Cursor>
{
    Cursor(CursorType type, string? name)
    {
        Type = type;
        Name = name;
    }

    /// <summary>A cursor the host knows by <paramref name="name"/>.</summary>
    public Cursor(string name)
        : this(CursorType.Custom, name) => ArgumentException.ThrowIfNullOrEmpty(name);

    public CursorType Type { get; }

    /// <summary>The name of a <see cref="CursorType.Custom"/> cursor; null for the others.</summary>
    public string? Name { get; }

    internal static Cursor Of(CursorType type) => new(type, null);

    public bool Equals(Cursor? other) =>
        other is not null
        && other.Type == Type
        && string.Equals(other.Name, Name, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as Cursor);

    public override int GetHashCode() => HashCode.Combine(Type, Name);

    public override string ToString() => Name ?? Type.ToString();
}

public static class Cursors
{
    static readonly Cursor[] _all = Create();

    public static Cursor Arrow => _all[(int)CursorType.Arrow];

    public static Cursor IBeam => _all[(int)CursorType.IBeam];

    public static Cursor Wait => _all[(int)CursorType.Wait];

    public static Cursor Crosshair => _all[(int)CursorType.Crosshair];

    public static Cursor WaitArrow => _all[(int)CursorType.WaitArrow];

    public static Cursor SizeNwSe => _all[(int)CursorType.SizeNwSe];

    public static Cursor SizeNeSw => _all[(int)CursorType.SizeNeSw];

    public static Cursor SizeWe => _all[(int)CursorType.SizeWe];

    public static Cursor SizeNs => _all[(int)CursorType.SizeNs];

    public static Cursor SizeAll => _all[(int)CursorType.SizeAll];

    public static Cursor No => _all[(int)CursorType.No];

    public static Cursor Hand => _all[(int)CursorType.Hand];

    public static Cursor ScrollAll => _all[(int)CursorType.ScrollAll];

    static Cursor[] Create()
    {
        var all = new Cursor[(int)CursorType.Custom];
        for (var i = 0; i < all.Length; i++)
            all[i] = Cursor.Of((CursorType)i);
        return all;
    }
}
