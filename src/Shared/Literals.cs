using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Tralys.Ui.CodeGen;

static class Literals
{
    public static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None
        && SyntaxFacts.GetContextualKeywordKind(name) == SyntaxKind.None
            ? name
            : "@" + name;

    public static string Quote(string value) => SymbolDisplay.FormatLiteral(value, quote: true);

    public static string Char(char value) => SymbolDisplay.FormatLiteral(value, quote: true);

    public static string Single(float value) =>
        float.IsNaN(value) ? "float.NaN"
        : float.IsPositiveInfinity(value) ? "float.PositiveInfinity"
        : float.IsNegativeInfinity(value) ? "float.NegativeInfinity"
        : value.ToString("R", CultureInfo.InvariantCulture) + "f";

    public static string Double(double value) =>
        double.IsNaN(value) ? "double.NaN"
        : double.IsPositiveInfinity(value) ? "double.PositiveInfinity"
        : double.IsNegativeInfinity(value) ? "double.NegativeInfinity"
        : value.ToString("R", CultureInfo.InvariantCulture) + "d";

    public static string Decimal(decimal value) =>
        value.ToString(CultureInfo.InvariantCulture) + "m";

    public static string? Integer(decimal value, SpecialType type)
    {
        // A literal of a small type is cast, since a generic parameter inferred from a bare one would be int.
        var (min, max, cast, suffix) = type switch
        {
            SpecialType.System_SByte => (sbyte.MinValue, sbyte.MaxValue, "(sbyte)", ""),
            SpecialType.System_Byte => (byte.MinValue, byte.MaxValue, "(byte)", ""),
            SpecialType.System_Int16 => (short.MinValue, short.MaxValue, "(short)", ""),
            SpecialType.System_UInt16 => (ushort.MinValue, ushort.MaxValue, "(ushort)", ""),
            SpecialType.System_Int32 => (int.MinValue, int.MaxValue, "", ""),
            SpecialType.System_UInt32 => (uint.MinValue, uint.MaxValue, "", "u"),
            SpecialType.System_Int64 => (long.MinValue, long.MaxValue, "", "L"),
            SpecialType.System_UInt64 => (0m, ulong.MaxValue, "", "ul"),
            _ => (0m, -1m, "", ""),
        };
        return value >= min && value <= max
            ? cast + value.ToString(CultureInfo.InvariantCulture) + suffix
            : null;
    }

    public static string Enum(INamedTypeSymbol type, object value)
    {
        var name = type.Fq();
        var bits = Bits(value);
        var members = new List<(string Name, long Bits)>();
        foreach (var member in type.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true, ConstantValue: { } constant })
                members.Add((Identifier(member.Name), Bits(constant)));
        }

        foreach (var member in members)
        {
            if (member.Bits == bits)
                return $"{name}.{member.Name}";
        }

        var parts = members.Where(m => m.Bits != 0 && (bits & m.Bits) == m.Bits).ToList();
        return parts.Count > 0 && parts.Aggregate(0L, (all, m) => all | m.Bits) == bits
            ? string.Join(" | ", parts.Select(m => $"{name}.{m.Name}"))
            : $"({name})({bits.ToString(CultureInfo.InvariantCulture)})";
    }

    static long Bits(object value) =>
        value is ulong wide
            ? unchecked((long)wide)
            : Convert.ToInt64(value, CultureInfo.InvariantCulture);
}
