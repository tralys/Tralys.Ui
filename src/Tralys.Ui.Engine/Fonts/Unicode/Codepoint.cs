using System.Runtime.CompilerServices;

namespace Tralys.Ui.Fonts.Unicode
{
    internal readonly record struct Codepoint
    {
        private readonly uint _value;

        /// <summary>
        /// The replacement codepoint that is used for non supported values.
        /// </summary>
        public static Codepoint ReplacementCodepoint
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new('�');
        }

        /// <summary>
        /// Creates a new instance of <see cref="Codepoint"/> with the specified value.
        /// </summary>
        /// <param name="value">The codepoint value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Codepoint(uint value) => _value = value;

        /// <summary>
        /// Get the codepoint's value.
        /// </summary>
        public uint Value => _value;

        /// <summary>
        /// Gets the <see cref="Unicode.GeneralCategory"/>.
        /// </summary>
        public GeneralCategory GeneralCategory => UnicodeData.GetGeneralCategory(_value);

        /// <summary>
        /// Gets the <see cref="Unicode.LineBreakClass"/>.
        /// </summary>
        public LineBreakClass LineBreakClass => UnicodeData.GetLineBreakClass(_value);

        /// <summary>
        /// Reads the <see cref="Codepoint"/> at specified position.
        /// </summary>
        /// <param name="text">The buffer to read from.</param>
        /// <param name="index">The index to read at.</param>
        /// <param name="count">The count of character that were read.</param>
        /// <returns></returns>
        [MethodImpl(
            MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization
        )]
        public static Codepoint ReadAt(ReadOnlySpan<char> text, int index, out int count)
        {
            // Perf note: this method is performance critical for text layout, modify with care!

            count = 1;

            // Perf note: uint check allows the JIT to ellide the next bound check
            if ((uint)index >= (uint)text.Length)
            {
                return ReplacementCodepoint;
            }

            uint code = text[index];

            //# Surrogate
            if (IsInRangeInclusive(code, 0xD800U, 0xDFFFU))
            {
                uint hi,
                    low;

                //# High surrogate
                if (code <= 0xDBFF)
                {
                    if ((uint)(index + 1) < (uint)text.Length)
                    {
                        hi = code;
                        low = text[index + 1];

                        if (IsInRangeInclusive(low, 0xDC00U, 0xDFFFU))
                        {
                            count = 2;
                            // Perf note: the code is written as below to become just two instructions: shl, lea.
                            // See https://github.com/dotnet/runtime/blob/7ec3634ee579d89b6024f72b595bfd7118093fc5/src/libraries/System.Private.CoreLib/src/System/Text/UnicodeUtility.cs#L38
                            return new Codepoint(
                                (hi << 10) + low - ((0xD800U << 10) + 0xDC00U - (1 << 16))
                            );
                        }
                    }
                }
                //# Low surrogate
                else
                {
                    if (index > 0)
                    {
                        low = code;
                        hi = text[index - 1];

                        if (IsInRangeInclusive(hi, 0xD800U, 0xDBFFU))
                        {
                            count = 2;
                            return new Codepoint(
                                (hi << 10) + low - ((0xD800U << 10) + 0xDC00U - (1 << 16))
                            );
                        }
                    }
                }

                return ReplacementCodepoint;
            }

            return new Codepoint(code);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound) =>
            value - lowerBound <= upperBound - lowerBound;

        /// <summary>
        /// Returns <see langword="true"/> if <paramref name="cp"/> is between
        /// <paramref name="lowerBound"/> and <paramref name="upperBound"/>, inclusive.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInRangeInclusive(Codepoint cp, uint lowerBound, uint upperBound) =>
            IsInRangeInclusive(cp._value, lowerBound, upperBound);
    }
}
