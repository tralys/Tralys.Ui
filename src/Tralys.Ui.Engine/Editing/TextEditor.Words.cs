using System.Globalization;
using System.Text;
using Tralys.Ui.Fonts;
using Tralys.Ui.Fonts.Unicode;

namespace Tralys.Ui;

internal sealed partial class TextEditor
{
    [FramePath]
    public int NextWord(int index)
    {
        if (_masked || index >= _length)
            return _length;

        index = Math.Max(0, index);
        var kind = KindAt(index);
        if (kind is WordKind.LineBreak or WordKind.Standalone)
            return NextCluster(index);

        if (kind != WordKind.Space)
            index = SkipForward(index, kind);
        return SkipForward(index, WordKind.Space);
    }

    [FramePath]
    public int PreviousWord(int index)
    {
        if (_masked)
            return 0;
        index = Math.Min(index, _length);
        var start = SkipBackward(index, WordKind.Space);
        if (start == 0)
            return 0;

        var word = PreviousCluster(start);
        var kind = KindAt(word);
        if (kind == WordKind.LineBreak && start < index)
            return start;
        if (kind is WordKind.LineBreak or WordKind.Standalone)
            return word;

        return SkipBackward(word, kind);
    }

    [FramePath]
    public (int Start, int End) WordAt(int index)
    {
        if (_masked || _length == 0)
            return (0, _length);

        index = Math.Clamp(index, 0, _length);
        if (index == _length || index > 0 && KindAt(index) == WordKind.LineBreak)
        {
            var before = PreviousCluster(index);
            if (index == _length || KindAt(before) != WordKind.LineBreak)
                index = before;
        }

        var kind = KindAt(index);
        var end = NextCluster(index);
        if (kind is WordKind.LineBreak or WordKind.Standalone)
            return (index, end);

        return (SkipBackward(index, kind), SkipForward(end, kind));
    }

    [FramePath]
    int SkipForward(int index, WordKind kind)
    {
        while (index < _length && KindAt(index) == kind)
            index = NextCluster(index);
        return index;
    }

    [FramePath]
    int SkipBackward(int index, WordKind kind)
    {
        while (index > 0)
        {
            var from = TextClusters.LastControl(Text, index);
            var run = from;
            for (var cluster = from; cluster < index; )
            {
                var next = NextCluster(cluster);
                if (KindAt(cluster) != kind)
                    run = next;
                cluster = next;
            }
            if (run > from)
                return Math.Min(run, index);
            index = from;
        }
        return 0;
    }

    [FramePath]
    WordKind KindAt(int index)
    {
        if (
            Rune.DecodeFromUtf16(Text[index..], out var rune, out _)
            != System.Buffers.OperationStatus.Done
        )
            return WordKind.Other;

        if (TextClusters.IsLineBreak(Text[index]))
            return WordKind.LineBreak;
        if (Rune.IsWhiteSpace(rune))
            return WordKind.Space;

        var category = Rune.GetUnicodeCategory(rune);
        if (
            category
            is UnicodeCategory.UppercaseLetter
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.DecimalDigitNumber
        )
            return WordKind.Letter;
        if (
            UnicodeData.GetLineBreakClass((uint)rune.Value)
            is LineBreakClass.Ideographic
                or LineBreakClass.ConditionalJapaneseStarter
                or LineBreakClass.EBase
                or LineBreakClass.EModifier
                or LineBreakClass.RegionalIndicator
        )
            return WordKind.Standalone;

        return category switch
        {
            UnicodeCategory.ModifierLetter
            or UnicodeCategory.OtherLetter
            or UnicodeCategory.LetterNumber
            or UnicodeCategory.OtherNumber
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.ConnectorPunctuation => WordKind.Letter,
            _ => WordKind.Other,
        };
    }

    enum WordKind : byte
    {
        Space,
        LineBreak,
        Letter,
        Standalone,
        Other,
    }
}
