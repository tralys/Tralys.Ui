using System.Globalization;
using Tralys.Ui.Fonts.Unicode;

namespace Tralys.Ui.Fonts;

static class TextClusters
{
    [FramePath]
    public static bool IsLineBreak(char c) =>
        UnicodeData.GetLineBreakClass(c)
            is LineBreakClass.MandatoryBreak
                or LineBreakClass.CarriageReturn
                or LineBreakClass.LineFeed
                or LineBreakClass.NextLine;

    [FramePath]
    public static int Next(ReadOnlySpan<char> text, int index, int end) =>
        index + StringInfo.GetNextTextElementLength(text[index..end]);

    [FramePath]
    public static int Previous(ReadOnlySpan<char> text, int index)
    {
        if (index <= 0)
            return 0;

        var cluster = LastControl(text, index);
        while (true)
        {
            var next = Next(text, cluster, text.Length);
            if (next >= index)
                return cluster;
            cluster = next;
        }
    }

    // An index inside a cluster moves past it, never back to its start.
    [FramePath]
    public static int Boundary(ReadOnlySpan<char> text, int index)
    {
        if (index <= 0)
            return 0;
        if (index >= text.Length)
            return text.Length;

        var cluster = LastControl(text, index);
        while (cluster < index)
            cluster = Next(text, cluster, text.Length);
        return cluster;
    }

    // Nothing joins a control but CR to LF, so clusters walked from here match those from the text's start.
    [FramePath]
    public static int LastControl(ReadOnlySpan<char> text, int index)
    {
        var last = index - 1;
        while (last > 0 && !char.IsControl(text[last]))
            last--;
        return last > 0 && text[last] == '\n' && text[last - 1] == '\r' ? last - 1 : last;
    }
}
