using System.Buffers.Binary;
using System.Numerics;

namespace Tralys.Ui.Fonts;

[FramePath]
sealed class KerningTable
{
    const uint GposTag = 0x47504F53;
    const uint KernTag = 0x6B65726E;
    const uint DefaultScriptTag = 0x44464C54;
    const uint LatinScriptTag = 0x6C61746E;
    const ushort XAdvance = 0x0004;

    readonly ReadOnlyMemory<byte> _gpos;
    readonly int[] _subtables;
    readonly int[] _lookupEnds;
    readonly ReadOnlyMemory<byte> _kern;
    readonly int[] _kernPairs;

    KerningTable(
        ReadOnlyMemory<byte> gpos,
        int[] subtables,
        int[] lookupEnds,
        ReadOnlyMemory<byte> kern,
        int[] kernPairs
    )
    {
        _gpos = gpos;
        _subtables = subtables;
        _lookupEnds = lookupEnds;
        _kern = kern;
        _kernPairs = kernPairs;
    }

    public bool IsEmpty => _lookupEnds.Length == 0 && _kernPairs.Length == 0;

    public static KerningTable Load(IFontFace face)
    {
        var gpos = face.GetTable(GposTag);
        var subtables = new List<int>();
        var lookupEnds = new List<int>();
        ReadGpos(gpos.Span, subtables, lookupEnds);
        if (lookupEnds.Count > 0)
            return new KerningTable(gpos, [.. subtables], [.. lookupEnds], default, []);

        var kern = face.GetTable(KernTag);
        var pairs = new List<int>();
        ReadKern(kern.Span, pairs);
        return new KerningTable(default, [], [], kern, [.. pairs]);
    }

    public int GetUnits(int left, int right)
    {
        if ((uint)left > ushort.MaxValue || (uint)right > ushort.MaxValue)
            return 0;

        var total = 0;
        if (_lookupEnds.Length > 0)
        {
            var gpos = _gpos.Span;
            var subtable = 0;
            foreach (var end in _lookupEnds)
            {
                for (; subtable < end; subtable++)
                {
                    if (TryPairPos(gpos, _subtables[subtable], left, right, out var value))
                    {
                        total += value;
                        break;
                    }
                }
                subtable = end;
            }
            return total;
        }

        var kern = _kern.Span;
        var key = ((uint)left << 16) | (uint)right;
        for (var i = 0; i < _kernPairs.Length; i += 2)
        {
            var record = FindKey(kern, _kernPairs[i], _kernPairs[i + 1], 6, key, wideKey: true);
            if (record >= 0)
                total += (short)U16(kern, record + 4);
        }
        return total;
    }

    static void ReadGpos(ReadOnlySpan<byte> gpos, List<int> subtables, List<int> lookupEnds)
    {
        if (gpos.Length < 10 || U16(gpos, 0) != 1)
            return;

        int scriptList = U16(gpos, 4);
        int featureList = U16(gpos, 6);
        int lookupList = U16(gpos, 8);
        int lookupCount = U16(gpos, lookupList);
        if (scriptList == 0 || featureList == 0 || lookupList == 0 || lookupCount == 0)
            return;

        var kernLookups = new bool[lookupCount];
        var langSys = DefaultLangSys(gpos, scriptList);
        if (langSys >= 0)
        {
            int required = U16(gpos, langSys + 2);
            if (required != 0xFFFF)
                MarkKernLookups(gpos, featureList, required, kernLookups);

            int featureCount = U16(gpos, langSys + 4);
            for (var i = 0; i < featureCount; i++)
                MarkKernLookups(gpos, featureList, U16(gpos, langSys + 6 + i * 2), kernLookups);
        }
        else
        {
            int featureCount = U16(gpos, featureList);
            for (var i = 0; i < featureCount; i++)
                MarkKernLookups(gpos, featureList, i, kernLookups);
        }

        for (var lookupIndex = 0; lookupIndex < lookupCount; lookupIndex++)
        {
            if (!kernLookups[lookupIndex])
                continue;

            var lookup = lookupList + U16(gpos, lookupList + 2 + lookupIndex * 2);
            int type = U16(gpos, lookup);
            int subtableCount = U16(gpos, lookup + 4);
            var before = subtables.Count;
            for (var i = 0; i < subtableCount; i++)
            {
                var subtable = lookup + U16(gpos, lookup + 6 + i * 2);
                var subtableType = type;
                if (type == 9 && U16(gpos, subtable) == 1)
                {
                    subtableType = U16(gpos, subtable + 2);
                    subtable += (int)Math.Min(U32(gpos, subtable + 4), int.MaxValue / 2);
                }

                if (subtableType == 2 && U16(gpos, subtable) is 1 or 2)
                    subtables.Add(subtable);
            }

            if (subtables.Count > before)
                lookupEnds.Add(subtables.Count);
        }
    }

    static int DefaultLangSys(ReadOnlySpan<byte> gpos, int scriptList)
    {
        int scriptCount = U16(gpos, scriptList);
        var fallback = -1;
        var latin = -1;
        for (var i = 0; i < scriptCount; i++)
        {
            var record = scriptList + 2 + i * 6;
            var script = scriptList + U16(gpos, record + 4);
            int offset = U16(gpos, script);
            if (offset == 0)
                continue;

            var tag = U32(gpos, record);
            if (tag == DefaultScriptTag)
                return script + offset;

            if (tag == LatinScriptTag)
                latin = script + offset;
            else if (fallback < 0)
                fallback = script + offset;
        }
        return latin >= 0 ? latin : fallback;
    }

    static void MarkKernLookups(
        ReadOnlySpan<byte> gpos,
        int featureList,
        int featureIndex,
        bool[] lookups
    )
    {
        if (featureIndex >= U16(gpos, featureList))
            return;

        var record = featureList + 2 + featureIndex * 6;
        if (U32(gpos, record) != KernTag)
            return;

        var feature = featureList + U16(gpos, record + 4);
        int count = U16(gpos, feature + 2);
        for (var i = 0; i < count; i++)
        {
            int lookup = U16(gpos, feature + 4 + i * 2);
            if (lookup < lookups.Length)
                lookups[lookup] = true;
        }
    }

    static bool TryPairPos(
        ReadOnlySpan<byte> gpos,
        int subtable,
        int left,
        int right,
        out int value
    )
    {
        value = 0;
        var coverage = CoverageIndex(gpos, subtable + U16(gpos, subtable + 2), left);
        if (coverage < 0)
            return false;

        int format1 = U16(gpos, subtable + 4);
        int format2 = U16(gpos, subtable + 6);
        if (U16(gpos, subtable) == 1)
        {
            if (coverage >= U16(gpos, subtable + 8))
                return false;

            var pairSet = subtable + U16(gpos, subtable + 10 + coverage * 2);
            var recordSize = 2 + ValueSize(format1) + ValueSize(format2);
            var record = FindKey(gpos, pairSet + 2, U16(gpos, pairSet), recordSize, (uint)right);
            if (record < 0)
                return false;

            value = XAdvanceOf(gpos, format1, record + 2);
            return true;
        }

        var class1 = ClassOf(gpos, subtable + U16(gpos, subtable + 8), left);
        var class2 = ClassOf(gpos, subtable + U16(gpos, subtable + 10), right);
        int class2Count = U16(gpos, subtable + 14);
        if (class1 >= U16(gpos, subtable + 12) || class2 >= class2Count)
            return false;

        var size = ValueSize(format1) + ValueSize(format2);
        value = XAdvanceOf(gpos, format1, subtable + 16 + (class1 * class2Count + class2) * size);
        return true;
    }

    static int CoverageIndex(ReadOnlySpan<byte> gpos, int coverage, int glyph)
    {
        int format = U16(gpos, coverage);
        int count = U16(gpos, coverage + 2);
        var first = coverage + 4;
        if (format == 1)
        {
            var record = FindKey(gpos, first, count, 2, (uint)glyph);
            return record < 0 ? -1 : (record - first) / 2;
        }

        if (format != 2)
            return -1;

        var range = FindRange(gpos, first, count, glyph);
        return range < 0 ? -1 : U16(gpos, range + 4) + glyph - U16(gpos, range);
    }

    static int ClassOf(ReadOnlySpan<byte> gpos, int classDef, int glyph)
    {
        int format = U16(gpos, classDef);
        if (format == 1)
        {
            var index = glyph - U16(gpos, classDef + 2);
            return index >= 0 && index < U16(gpos, classDef + 4)
                ? U16(gpos, classDef + 6 + index * 2)
                : 0;
        }

        if (format != 2)
            return 0;

        var range = FindRange(gpos, classDef + 4, U16(gpos, classDef + 2), glyph);
        return range < 0 ? 0 : U16(gpos, range + 4);
    }

    static int FindKey(
        ReadOnlySpan<byte> data,
        int first,
        int count,
        int size,
        uint key,
        bool wideKey = false
    )
    {
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var record = first + middle * size;
            var candidate = wideKey ? U32(data, record) : U16(data, record);
            if (candidate == key)
                return record;
            if (candidate < key)
                low = middle + 1;
            else
                high = middle - 1;
        }
        return -1;
    }

    static int FindRange(ReadOnlySpan<byte> data, int first, int count, int glyph)
    {
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = (low + high) >>> 1;
            var range = first + middle * 6;
            if (glyph < U16(data, range))
                high = middle - 1;
            else if (glyph > U16(data, range + 2))
                low = middle + 1;
            else
                return range;
        }
        return -1;
    }

    static int ValueSize(int format) => BitOperations.PopCount((uint)format & 0xFF) * 2;

    static int XAdvanceOf(ReadOnlySpan<byte> gpos, int format, int record) =>
        (format & XAdvance) == 0
            ? 0
            : (short)U16(gpos, record + BitOperations.PopCount((uint)format & 0x3) * 2);

    static void ReadKern(ReadOnlySpan<byte> kern, List<int> pairs)
    {
        if (kern.Length < 4)
            return;

        if (U16(kern, 0) == 0)
        {
            int count = U16(kern, 2);
            var offset = 4;
            for (var i = 0; i < count && offset + 6 <= kern.Length; i++)
            {
                int coverage = U16(kern, offset + 4);
                int pairCount = U16(kern, offset + 6);
                // Horizontal, not minimum or cross-stream, format 0.
                if ((coverage & 0xFF07) == 0x0001)
                    AddPairs(kern, offset + 14, pairCount, pairs);

                // The u16 length overflows for a large format-0 subtable, so its size is taken from its pair count.
                offset +=
                    (coverage >> 8) == 0
                        ? 14 + pairCount * 6
                        : Math.Max(6, (int)U16(kern, offset + 2));
            }
            return;
        }

        if (U32(kern, 0) != 0x00010000)
            return;

        var tables = U32(kern, 4);
        var position = 8;
        for (uint i = 0; i < tables && position + 8 <= kern.Length; i++)
        {
            var length = (int)Math.Min(U32(kern, position), int.MaxValue / 2);
            int coverage = U16(kern, position + 4);
            // Horizontal, not cross-stream or variation, format 0.
            if ((coverage & 0xE0FF) == 0)
                AddPairs(kern, position + 16, U16(kern, position + 8), pairs);

            if (length < 8)
                break;
            position += length;
        }
    }

    static void AddPairs(ReadOnlySpan<byte> kern, int offset, int count, List<int> pairs)
    {
        count = Math.Min(count, Math.Max(0, (kern.Length - offset) / 6));
        if (count <= 0)
            return;

        pairs.Add(offset);
        pairs.Add(count);
    }

    // An out-of-range read is 0, so a malformed table kerns nothing instead of throwing.
    static ushort U16(ReadOnlySpan<byte> data, int offset) =>
        offset >= 0 && offset <= data.Length - 2
            ? BinaryPrimitives.ReadUInt16BigEndian(data[offset..])
            : (ushort)0;

    static uint U32(ReadOnlySpan<byte> data, int offset) =>
        offset >= 0 && offset <= data.Length - 4
            ? BinaryPrimitives.ReadUInt32BigEndian(data[offset..])
            : 0u;
}
