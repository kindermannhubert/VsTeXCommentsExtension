using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace VsTeXCommentsExtension
{
    internal static class Extensions
    {
        public static bool ConsistOnlyFromLineBreaks(this string text) => text.ConsistOnlyFromLineBreaks(0, text.Length);

        public static bool ConsistOnlyFromLineBreaks(this string text, int start, int length)
        {
            int end = start + length;
            for (int i = start; i < end; i++)
            {
                var ch = text[i];
                if (ch != '\r' && ch != '\n') return false;
            }

            return true;
        }

        public static Span TranslateStart(this Span span, int offset) => new Span(span.Start + offset, span.Length - offset);
        public static Span TranslateEnd(this Span span, int offset) => new Span(span.Start, span.Length + offset);

        public static int NumberOfWhiteSpaceCharsOnStartOfLine(this string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch != ' ' && ch != '\t') return i;
            }
            return 0;
        }

        public static int NumberOfWhiteSpaceCharsOnStartOfLine(this ITextSnapshot snapshot, int lineStartPosition, int lineLength)
        {
            for (int i = 0; i < lineLength; i++)
            {
                var ch = snapshot[lineStartPosition + i];
                if (ch != ' ' && ch != '\t') return i;
            }
            return 0;
        }

        public static bool StartsWith(this Span<char> text, string value)
        {
            int endIndex = value.Length - 1;
            if (text.Length <= endIndex) return false;

            for (int i = 0, j = 0; i <= endIndex; ++i, ++j)
            {
                if (text[i] != value[j]) return false;
            }
            return true;
        }

        public static void AddRange<T>(this List<T> list, PooledStructEnumerable<T> values)
        {
            foreach (var value in values)
            {
                list.Add(value);
            }
        }

        public static void AddRangeByFor<T>(this IList<T> list, IList<T> values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                list.Add(values[i]);
            }
        }
    }
}