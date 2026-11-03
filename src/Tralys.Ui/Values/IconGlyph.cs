using System.Collections.Concurrent;
using System.Text;

namespace Tralys.Ui;

/// <summary>The glyphs Tralys.Ui's own components draw. The app's icon set names the glyph each role draws in its icon
/// fonts, which the icon compiler checks at build time, so no component carries a codepoint.</summary>
public enum IconRole
{
    /// <summary>The turning glyph of a spinner.</summary>
    Loader,

    /// <summary>The arrow a disclosure turns.</summary>
    ChevronRight,

    /// <summary>The glyph of an icon that explains what it sits beside.</summary>
    Info,

    /// <summary>The cross of a close button.</summary>
    X,

    /// <summary>The mark of a done status badge.</summary>
    Check,

    /// <summary>The padlock of a locked status badge.</summary>
    Lock,

    /// <summary>The glass a search field leads with.</summary>
    Search,

    /// <summary>The step down of a number field.</summary>
    Minus,

    /// <summary>The step up of a number field.</summary>
    Plus,

    /// <summary>The arrow a menu or picker trigger turns over while its popup is open.</summary>
    ChevronDown,

    /// <summary>The back key of a number pad, which deletes the last digit.</summary>
    Delete,

    /// <summary>The upward arrow of a touch stick.</summary>
    ChevronUp,

    /// <summary>The leftward arrow of a touch stick, and a pagination's step to the previous page.</summary>
    ChevronLeft,

    /// <summary>The mark of a column header sorting its column ascending.</summary>
    ArrowUp,

    /// <summary>The mark of a column header sorting its column descending.</summary>
    ArrowDown,

    /// <summary>A pagination's jump to the first page.</summary>
    ChevronsLeft,

    /// <summary>A pagination's jump to the last page.</summary>
    ChevronsRight,

    /// <summary>The mark of a toast telling of a success.</summary>
    CheckCircle,

    /// <summary>The mark of a toast telling of an error.</summary>
    XCircle,

    /// <summary>The mark of a toast giving a warning.</summary>
    AlertTriangle,

    /// <summary>The funnel a filter button shows.</summary>
    Filter,

    /// <summary>A collection's switch to its items as a grid of tiles.</summary>
    Grid,

    /// <summary>A collection's switch to its items as a list of rows.</summary>
    List,
}

/// <summary>One glyph of an icon font: a codepoint in the font family that draws it, or an <see cref="IconRole"/> the
/// context's <see cref="UiContext.Icons"/> resolves. The app's icon compiler emits one per icon its icon set names; the
/// default draws nothing.</summary>
public readonly struct IconGlyph : IEquatable<IconGlyph>
{
    internal static readonly int RoleCount = Enum.GetValues<IconRole>().Length;

    static readonly ConcurrentDictionary<int, string> s_texts = new();

    readonly string? _text;
    readonly int _role;

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="codepoint"/> is 0 or no Unicode
    /// character.</exception>
    public IconGlyph(int codepoint, string fontFamily)
    {
        if (codepoint == 0 || !Rune.IsValid(codepoint))
            throw new ArgumentOutOfRangeException(
                nameof(codepoint),
                codepoint,
                "A glyph needs a valid codepoint."
            );
        ArgumentException.ThrowIfNullOrEmpty(fontFamily);
        Codepoint = codepoint;
        FontFamily = fontFamily;
        _text = s_texts.GetOrAdd(codepoint, static c => new Rune(c).ToString());
    }

    IconGlyph(IconRole role) => _role = (int)role + 1;

    /// <summary>The glyph the context's icon set draws for <paramref name="role"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="role"/> is no role.</exception>
    public static IconGlyph Of(IconRole role) =>
        (uint)role < (uint)RoleCount ? new(role) : throw NoRole(role);

    static ArgumentOutOfRangeException NoRole(IconRole role) =>
        new(nameof(role), role, "No such icon role.");

    /// <summary>The codepoint; 0 for a role or none.</summary>
    public int Codepoint { get; }

    /// <summary>The family of the icon font that draws the glyph; null for a role or none.</summary>
    public string? FontFamily { get; }

    /// <summary>The role this glyph stands for; null for a glyph of its own or none.</summary>
    public IconRole? Role => _role == 0 ? null : (IconRole)(_role - 1);

    public bool IsEmpty => Codepoint == 0 && _role == 0;

    /// <summary>The glyph as text, to draw in <see cref="FontFamily"/>; null for a role or none.</summary>
    public TextValue Text => _text;

    public bool Equals(IconGlyph other) =>
        Codepoint == other.Codepoint
        && _role == other._role
        && string.Equals(FontFamily, other.FontFamily, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is IconGlyph other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Codepoint, _role);

    public static bool operator ==(IconGlyph left, IconGlyph right) => left.Equals(right);

    public static bool operator !=(IconGlyph left, IconGlyph right) => !left.Equals(right);

    public override string ToString() =>
        Role is { } role ? role.ToString()
        : Codepoint == 0 ? "(none)"
        : $"U+{Codepoint:X4} {FontFamily}";
}

/// <summary>The glyph an app's icon set draws for each <see cref="IconRole"/>, which its icon compiler emits as
/// <c>Icons.Set</c>. Immutable, so contexts on any thread share one.</summary>
public sealed class IconSet
{
    readonly IconGlyph[] _roles;

    /// <param name="roles">The glyph of every role, in the order <see cref="IconRole"/> declares them.</param>
    /// <exception cref="ArgumentException">A role is missing or extra, or given as a role or as none.</exception>
    public IconSet(ReadOnlySpan<IconGlyph> roles)
    {
        if (roles.Length != IconGlyph.RoleCount)
            throw new ArgumentException(
                $"An icon set names a glyph for each of the {IconGlyph.RoleCount} roles.",
                nameof(roles)
            );
        foreach (var glyph in roles)
        {
            if (glyph.Codepoint == 0)
                throw new ArgumentException(
                    "Each role needs a glyph of its own, not a role or none.",
                    nameof(roles)
                );
        }
        _roles = roles.ToArray();
    }

    /// <summary>The glyph this set draws for <paramref name="role"/>.</summary>
    [FramePath]
    public IconGlyph Glyph(IconRole role) => _roles[(int)role];
}
