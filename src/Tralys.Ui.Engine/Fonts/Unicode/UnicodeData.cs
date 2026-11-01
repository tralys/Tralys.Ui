using System.Runtime.CompilerServices;

namespace Tralys.Ui.Fonts.Unicode
{
    /// <summary>
    ///     Helper for looking up unicode character class information
    /// </summary>
    internal static class UnicodeData
    {
        internal const int CATEGORY_BITS = 6;
        internal const int SCRIPT_BITS = 8;
        internal const int LINEBREAK_BITS = 6;

        internal const int LINEBREAK_SHIFT = CATEGORY_BITS + SCRIPT_BITS;

        internal const int CATEGORY_MASK = (1 << CATEGORY_BITS) - 1;
        internal const int LINEBREAK_MASK = (1 << LINEBREAK_BITS) - 1;

        /// <summary>
        /// Gets the <see cref="GeneralCategory"/> for a Unicode codepoint.
        /// </summary>
        /// <param name="codepoint">The codepoint in question.</param>
        /// <returns>The code point's general category.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GeneralCategory GetGeneralCategory(uint codepoint)
        {
            return (GeneralCategory)(UnicodeDataTrie.Trie.Get(codepoint) & CATEGORY_MASK);
        }

        /// <summary>
        /// Gets the line break class for a Unicode codepoint. A code point the data leaves out, unassigned or past its
        /// last plane, takes UAX #14's default for its range: ID in the ideograph and pictograph ranges, PR among the
        /// currency symbols, else XX.
        /// </summary>
        /// <param name="codepoint">The codepoint in question.</param>
        /// <returns>The code point's line break class.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [FramePath]
        public static LineBreakClass GetLineBreakClass(uint codepoint)
        {
            var value = UnicodeDataTrie.Trie.Get(codepoint);
            var cls = (LineBreakClass)((value >> LINEBREAK_SHIFT) & LINEBREAK_MASK);
            return
                cls == LineBreakClass.OpenPunctuation
                && (GeneralCategory)(value & CATEGORY_MASK)
                    is GeneralCategory.Unassigned
                        or GeneralCategory.Other
                ? MissingLineBreakClass(codepoint)
                : cls;
        }

        [FramePath]
        private static LineBreakClass MissingLineBreakClass(uint codepoint) =>
            codepoint switch
            {
                (>= 0x3400 and <= 0x4DBF)
                or (>= 0x4E00 and <= 0x9FFF)
                or (>= 0xF900 and <= 0xFAFF)
                or (>= 0x20000 and <= 0x2FFFD)
                or (>= 0x30000 and <= 0x3FFFD)
                or (>= 0x1F000 and <= 0x1FAFF)
                or (>= 0x1FC00 and <= 0x1FFFD) => LineBreakClass.Ideographic,
                >= 0x20A0 and <= 0x20CF => LineBreakClass.PrefixNumeric,
                _ => LineBreakClass.Unknown,
            };
    }
}
