using System.Globalization;
using System.Text;

namespace Tralys.Ui.CodeGen;

// An analyzer cannot load a JSON dependency, so the generators parse their own.
static class Json
{
    public static bool TryParseObject(
        string text,
        out Dictionary<string, object?> root,
        out string error
    )
    {
        root = new Dictionary<string, object?>(StringComparer.Ordinal);
        error = "";
        object? parsed;
        try
        {
            var i = 0;
            parsed = Value(text, ref i);
            Skip(text, ref i);
            if (i < text.Length)
                throw new FormatException($"unexpected '{text[i]}' after the root value");
        }
        catch (FormatException e)
        {
            error = e.Message;
            return false;
        }

        if (parsed is not Dictionary<string, object?> map)
        {
            error = "the root is not a JSON object";
            return false;
        }

        root = map;
        return true;
    }

    static object? Value(string s, ref int i)
    {
        Skip(s, ref i);
        if (i >= s.Length)
            throw new FormatException("unexpected end of input");

        switch (s[i])
        {
            case '{':
                return Object(s, ref i);
            case '[':
                return Array(s, ref i);
            case '"':
                return String(s, ref i);
            case 't':
                Expect(s, ref i, "true");
                return true;
            case 'f':
                Expect(s, ref i, "false");
                return false;
            case 'n':
                Expect(s, ref i, "null");
                return null;
            default:
                return Number(s, ref i);
        }
    }

    static Dictionary<string, object?> Object(string s, ref int i)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        i++;
        Skip(s, ref i);
        if (i < s.Length && s[i] == '}')
        {
            i++;
            return map;
        }

        while (true)
        {
            Skip(s, ref i);
            var key = String(s, ref i);
            if (map.ContainsKey(key))
                throw new FormatException($"duplicate key \"{key}\"");
            Skip(s, ref i);
            if (i >= s.Length || s[i] != ':')
                throw new FormatException($"expected ':' after \"{key}\"");
            i++;
            map[key] = Value(s, ref i);
            Skip(s, ref i);
            if (i >= s.Length)
                throw new FormatException("unterminated object");
            if (s[i] == ',')
            {
                i++;
                continue;
            }
            if (s[i] == '}')
            {
                i++;
                return map;
            }
            throw new FormatException("expected ',' or '}'");
        }
    }

    static List<object?> Array(string s, ref int i)
    {
        var items = new List<object?>();
        i++;
        Skip(s, ref i);
        if (i < s.Length && s[i] == ']')
        {
            i++;
            return items;
        }

        while (true)
        {
            items.Add(Value(s, ref i));
            Skip(s, ref i);
            if (i >= s.Length)
                throw new FormatException("unterminated array");
            if (s[i] == ',')
            {
                i++;
                continue;
            }
            if (s[i] == ']')
            {
                i++;
                return items;
            }
            throw new FormatException("expected ',' or ']'");
        }
    }

    static string String(string s, ref int i)
    {
        if (i >= s.Length || s[i] != '"')
            throw new FormatException("expected a string");
        i++;
        var text = new StringBuilder();
        while (i < s.Length && s[i] != '"')
        {
            if (s[i] != '\\')
            {
                text.Append(s[i++]);
                continue;
            }

            i++;
            if (i >= s.Length)
                break;
            switch (s[i])
            {
                case 'n':
                    text.Append('\n');
                    break;
                case 't':
                    text.Append('\t');
                    break;
                case 'r':
                    text.Append('\r');
                    break;
                case 'b':
                    text.Append('\b');
                    break;
                case 'f':
                    text.Append('\f');
                    break;
                case 'u' when i + 4 < s.Length:
                    text.Append(
                        (char)
                            int.Parse(
                                s.Substring(i + 1, 4),
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture
                            )
                    );
                    i += 4;
                    break;
                default:
                    text.Append(s[i]);
                    break;
            }
            i++;
        }
        if (i >= s.Length)
            throw new FormatException("unterminated string");
        i++;
        return text.ToString();
    }

    static double Number(string s, ref int i)
    {
        var start = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] is '-' or '+' or '.' or 'e' or 'E'))
            i++;
        if (start == i)
            throw new FormatException($"unexpected '{s[start]}'");
        var text = s.Substring(start, i - start);
        // .NET Framework refuses a number beyond a double where .NET reads it as infinity.
        if (
            !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || double.IsInfinity(number)
        )
            throw new FormatException($"'{text}' is not a number a double holds");
        return number;
    }

    static void Expect(string s, ref int i, string literal)
    {
        if (
            i + literal.Length > s.Length
            || string.CompareOrdinal(s, i, literal, 0, literal.Length) != 0
        )
            throw new FormatException($"expected '{literal}'");
        i += literal.Length;
    }

    static void Skip(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i]))
            i++;
    }
}
