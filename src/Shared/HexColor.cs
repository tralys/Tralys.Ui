namespace Tralys.Ui;

// '#' then RGB, ARGB, RRGGBB or AARRGGBB hex digits: alpha first, opaque when left out.
static class HexColor
{
    public static bool TryParse(ReadOnlySpan<char> text, out uint argb)
    {
        argb = 0;
        var digits = text.Length - 1;
        if (digits is not (3 or 4 or 6 or 8) || text[0] != '#')
            return false;

        uint value = 0;
        for (var i = 1; i < text.Length; i++)
        {
            var nibble = Nibble(text[i]);
            if (nibble < 0)
                return false;
            value = (value << 4) | (uint)nibble;
        }

        if (digits <= 4)
        {
            var doubled = 0u;
            for (var shift = (digits - 1) * 4; shift >= 0; shift -= 4)
                doubled = (doubled << 8) | (((value >> shift) & 0xF) * 0x11);
            value = doubled;
        }
        argb = digits is 3 or 6 ? 0xFF000000 | value : value;
        return true;
    }

    static int Nibble(char c) =>
        c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
}
