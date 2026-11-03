using System.Runtime.CompilerServices;

namespace Tralys.Ui;

internal static class EnumText<T>
{
    // Raw bits, zero-extended, ascending by unsigned magnitude as Enum.GetNames orders them.
    static readonly ulong[] Values;
    static readonly string[] Names;
    static readonly bool Flags;
    static readonly bool Signed;

    static EnumText()
    {
        var type = typeof(T);
        Names = Enum.GetNames(type);
        var raw = Enum.GetValuesAsUnderlyingType(type);
        Values = new ulong[raw.Length];
        for (var i = 0; i < raw.Length; i++)
            Values[i] = raw.GetValue(i) switch
            {
                sbyte v => (byte)v,
                short v => (ushort)v,
                int v => (uint)v,
                long v => (ulong)v,
                byte v => v,
                ushort v => v,
                uint v => v,
                ulong v => v,
                _ => 0,
            };
        var underlying = Enum.GetUnderlyingType(type);
        Signed =
            underlying == typeof(sbyte)
            || underlying == typeof(short)
            || underlying == typeof(int)
            || underlying == typeof(long);
        Flags = type.IsDefined(typeof(FlagsAttribute), inherit: false);
    }

    [FramePath]
    public static bool TryAppend(FrameArena arena, T value, ReadOnlySpan<char> format)
    {
        bool names;
        bool flags;
        if (format.IsEmpty || format is "G" or "g")
        {
            names = true;
            flags = Flags;
        }
        else if (format is "F" or "f")
        {
            names = true;
            flags = true;
        }
        else if (format is "D" or "d")
            names = flags = false;
        else
            return false;

        var bits = Bits(value);
        if (names && Name(bits) is { } name)
        {
            arena.Append(name);
            return true;
        }
        if (names && flags && bits != 0 && AppendFlags(arena, bits))
            return true;
        if (Signed)
            arena.AppendFormatted(SignExtended(bits), default);
        else
            arena.AppendFormatted(bits, default);
        return true;
    }

    static string? Name(ulong bits)
    {
        var index = Array.BinarySearch(Values, bits);
        if (index < 0)
            return null;
        while (index > 0 && Values[index - 1] == bits)
            index--;
        return Names[index];
    }

    // As Enum.ToString: the largest named values first, listed from the smallest; any bit no name covers makes it a number.
    static bool AppendFlags(FrameArena arena, ulong bits)
    {
        Span<int> taken = stackalloc int[64];
        var count = 0;
        var left = bits;
        for (var i = Values.Length - 1; i >= 0 && left != 0 && count < taken.Length; i--)
        {
            var value = Values[i];
            if (value != 0 && (left & value) == value)
            {
                taken[count++] = i;
                left &= ~value;
            }
        }
        if (left != 0)
            return false;
        for (var k = count - 1; k >= 0; k--)
        {
            arena.Append(Names[taken[k]]);
            if (k > 0)
                arena.Append(", ");
        }
        return true;
    }

    static ulong Bits(T value) =>
        Unsafe.SizeOf<T>() switch
        {
            1 => Unsafe.As<T, byte>(ref value),
            2 => Unsafe.As<T, ushort>(ref value),
            4 => Unsafe.As<T, uint>(ref value),
            _ => Unsafe.As<T, ulong>(ref value),
        };

    static long SignExtended(ulong bits) =>
        Unsafe.SizeOf<T>() switch
        {
            1 => (sbyte)(byte)bits,
            2 => (short)(ushort)bits,
            4 => (int)(uint)bits,
            _ => (long)bits,
        };
}
