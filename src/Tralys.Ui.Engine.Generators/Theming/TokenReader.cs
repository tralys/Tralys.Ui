using Tralys.Ui.CodeGen;

namespace Tralys.Ui.Generators.Theming;

enum TokenKind
{
    Color,
    Dimension,
    Duration,
    CubicBezier,
    FontFamily,
    FontWeight,
    Other,
}

// A colour composed from another by $ref is an alias with an alpha of its own; a plain alias has none.
sealed class Token(string path, TokenKind kind)
{
    public string Path { get; } = path;

    public TokenKind Kind { get; } = kind;

    public string? Alias { get; set; }

    public uint? Argb { get; set; }

    public double? Alpha { get; set; }

    // Seconds for a duration, whichever unit it was written in.
    public double Number { get; set; }

    public string? Family { get; set; }

    public double[]? Bezier { get; set; }
}

static class TokenReader
{
    static readonly HashSet<string> DtcgTypes = new(StringComparer.Ordinal)
    {
        "color",
        "dimension",
        "fontFamily",
        "fontWeight",
        "duration",
        "cubicBezier",
        "number",
        "strokeStyle",
        "border",
        "transition",
        "shadow",
        "gradient",
        "typography",
    };

    public static List<Token> Read(string content, List<string> errors, string file)
    {
        var tokens = new List<Token>();
        if (!Json.TryParseObject(content, out var root, out var error))
            errors.Add($"{file}: {error}");
        else
            Walk(root, "", null, tokens, errors, file);
        return tokens;
    }

    // $extensions is an object, so walking it as a group would read its members as tokens.
    static void Walk(
        Dictionary<string, object?> node,
        string prefix,
        string? inheritedType,
        List<Token> tokens,
        List<string> errors,
        string file
    )
    {
        var type = node.TryGetValue("$type", out var declared) ? declared as string : inheritedType;
        if (node.TryGetValue("$value", out var value))
        {
            if (type is null || !DtcgTypes.Contains(type))
            {
                errors.Add(
                    $"{file}: '{prefix}' has $type '{type ?? "(none)"}', which is not a DTCG type"
                );
                return;
            }
            tokens.Add(Token(prefix, type, value, errors, file));
            return;
        }

        foreach (var pair in node)
        {
            if (pair.Key.Length > 0 && pair.Key[0] == '$')
                continue;
            if (pair.Value is Dictionary<string, object?> child)
                Walk(
                    child,
                    prefix.Length == 0 ? pair.Key : prefix + "." + pair.Key,
                    type,
                    tokens,
                    errors,
                    file
                );
        }
    }

    static Token Token(string path, string type, object? value, List<string> errors, string file)
    {
        var kind = type switch
        {
            "color" => TokenKind.Color,
            "dimension" => TokenKind.Dimension,
            "duration" => TokenKind.Duration,
            "cubicBezier" => TokenKind.CubicBezier,
            "fontFamily" => TokenKind.FontFamily,
            "fontWeight" => TokenKind.FontWeight,
            _ => TokenKind.Other,
        };
        var token = new Token(path, kind);
        if (kind == TokenKind.Other)
            return token;

        if (Alias(value) is { } alias)
        {
            token.Alias = alias;
            return token;
        }

        var problem = kind switch
        {
            TokenKind.Color => ReadColor(token, value),
            TokenKind.Dimension => ReadDimension(token, value),
            TokenKind.Duration => ReadDuration(token, value),
            TokenKind.CubicBezier => ReadBezier(token, value),
            TokenKind.FontFamily => ReadFamily(token, value),
            _ => ReadWeight(token, value),
        };
        if (problem is not null)
            errors.Add($"{file}: '{path}' {problem}");
        return token;
    }

    static string? Alias(object? value) =>
        value is string { Length: > 2 } text && text[0] == '{' && text[text.Length - 1] == '}'
            ? text.Substring(1, text.Length - 2)
            : null;

    static string? ReadColor(Token token, object? value)
    {
        if (value is not Dictionary<string, object?> map)
            return "has a $value that is neither an alias nor a colour object";

        token.Alpha = map.TryGetValue("alpha", out var alpha) && alpha is double a ? a : 1;

        if (map.TryGetValue("hex", out var hex) && hex is string text)
        {
            if (!HexColor.TryParse(text.AsSpan(), out var argb))
                return $"has hex '{text}', which is not #RGB, #ARGB, #RRGGBB or #AARRGGBB";
            token.Argb = argb;
            return null;
        }

        if (
            Pointer(hex, "hex") is { } hexOf
            && map.TryGetValue("components", out var components)
            && Pointer(components, "components") is { } componentsOf
        )
        {
            if (hexOf != componentsOf)
                return "composes its hex and components from different tokens";
            token.Alias = hexOf;
            return null;
        }

        return "has no hex member";
    }

    static string? Pointer(object? value, string member)
    {
        var suffix = "/$value/" + member;
        if (
            value is not Dictionary<string, object?> reference
            || !reference.TryGetValue("$ref", out var target)
            || target is not string pointer
            || !pointer.StartsWith("#/", StringComparison.Ordinal)
            || !pointer.EndsWith(suffix, StringComparison.Ordinal)
            || pointer.Length <= 2 + suffix.Length
        )
            return null;

        return pointer.Substring(2, pointer.Length - 2 - suffix.Length).Replace('/', '.');
    }

    static string? ReadDimension(Token token, object? value)
    {
        if (
            value is not Dictionary<string, object?> map
            || !map.TryGetValue("value", out var number)
            || number is not double d
        )
            return "has no numeric value member";
        if (!map.TryGetValue("unit", out var unit) || unit as string != "px")
            return "is not in px";
        token.Number = d;
        return null;
    }

    static string? ReadDuration(Token token, object? value)
    {
        if (
            value is not Dictionary<string, object?> map
            || !map.TryGetValue("value", out var number)
            || number is not double d
        )
            return "has no numeric value member";

        switch (map.TryGetValue("unit", out var unit) ? unit as string : null)
        {
            case "ms":
                token.Number = d / 1000;
                return null;
            case "s":
                token.Number = d;
                return null;
            default:
                return "is in neither ms nor s";
        }
    }

    static string? ReadBezier(Token token, object? value)
    {
        if (value is not List<object?> { Count: 4 } points)
            return "is not an array of four numbers";

        var bezier = new double[4];
        for (var i = 0; i < 4; i++)
        {
            if (points[i] is not double d)
                return "is not an array of four numbers";
            bezier[i] = d;
        }
        if (bezier[0] < 0 || bezier[0] > 1 || bezier[2] < 0 || bezier[2] > 1)
            return "has an x outside 0 to 1";
        if (bezier[1] < 0 || bezier[1] > 1 || bezier[3] < 0 || bezier[3] > 1)
            return "has a y outside 0 to 1";
        token.Bezier = bezier;
        return null;
    }

    static string? ReadFamily(Token token, object? value)
    {
        if (value is not string { Length: > 0 } family)
            return "is not a single family name";
        token.Family = family;
        return null;
    }

    static string? ReadWeight(Token token, object? value)
    {
        if (
            value is not double weight
            || weight < 1
            || weight > 999
            || weight != Math.Floor(weight)
        )
            return "is not a weight from 1 to 999";
        token.Number = weight;
        return null;
    }
}
