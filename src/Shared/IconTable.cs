using System.Globalization;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Tralys.Ui.CodeGen;

readonly record struct IconEntry(string Name, int Codepoint, string Family);

// TralysIconFonts pairs split on commas: a semicolon would start a comment in the analyzer config.
static class IconTable
{
    public const string FontsProperty = "build_property.TralysIconFonts";

    public static bool IsIconFile(string path) =>
        path.EndsWith("IconCodepoints.json", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".icons.json", StringComparison.OrdinalIgnoreCase);

    public static string Fonts(AnalyzerConfigOptionsProvider options) =>
        options.GlobalOptions.TryGetValue(FontsProperty, out var value) ? value : "";

    public static EquatableArray<IconEntry> Read(string text, string fonts, out string? error)
    {
        error = null;
        var families = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in fonts.Split([','], StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');
            var key = equals < 0 ? "" : pair.Substring(0, equals).Trim();
            var family = equals < 0 ? "" : pair.Substring(equals + 1).Trim();
            if (key.Length == 0 || family.Length == 0)
            {
                error = $"TralysIconFonts entry '{pair}' is not key=family";
                return default;
            }
            families[key] = family;
        }

        if (!Json.TryParseObject(text, out var root, out var parseError))
        {
            error = parseError;
            return default;
        }

        var entries = new List<IconEntry>(root.Count);
        foreach (var pair in root)
        {
            error = Entry(pair.Key, pair.Value, families, entries);
            if (error is not null)
                return default;
        }
        return new EquatableArray<IconEntry>([.. entries]);
    }

    public static string GlyphExpression(IconEntry entry, string ns) =>
        $"new global::{ns}.IconGlyph(0x{entry.Codepoint:X4}, {Literals.Quote(entry.Family)})";

    static string? Entry(
        string name,
        object? value,
        Dictionary<string, string> families,
        List<IconEntry> entries
    )
    {
        if (value is not Dictionary<string, object?> fields)
            return $"'{name}' is not a {{ \"cp\", \"font\" }} object";

        foreach (var key in fields.Keys)
        {
            if (key is not ("cp" or "font"))
                return $"'{name}' has an unknown field '{key}'";
        }

        if (
            !fields.TryGetValue("cp", out var cp)
            || cp is not double codepoint
            || !fields.TryGetValue("font", out var named)
            || named is not string font
        )
            return $"'{name}' needs a cp and a font";

        if (codepoint == 0)
            return null;
        if (
            codepoint != Math.Floor(codepoint)
            || codepoint is < 0 or (>= 0xD800 and <= 0xDFFF) or > 0x10FFFF
        )
            return $"'{name}' has codepoint {codepoint.ToString(CultureInfo.InvariantCulture)}, which is no Unicode character";
        if (!families.TryGetValue(font, out var family))
            return $"'{name}' is in font '{font}', which TralysIconFonts maps to no family";

        entries.Add(new IconEntry(name, (int)codepoint, family));
        return null;
    }
}
