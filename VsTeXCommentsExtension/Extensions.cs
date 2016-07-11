using Microsoft.VisualStudio.Text;
using System.Collections;
using System.Collections.Generic;

namespace VsTeXCommentsExtension
{
    internal static class Extensions
    {
        public static bool ConsistOnlyFromWhiteSpaces(this string text) => text.ConsistOnlyFromWhiteSpaces(0, text.Length);

        public static bool ConsistOnlyFromWhiteSpaces(this string text, int start, int length)
        {
            int end = start + length;
            for (int i = start; i < end; i++)
            {
                var ch = text[i];
                if (ch != ' ' && ch != '\t' && ch != '\r' && ch != '\n') return false;
            }

            return true;
        }

        public static bool ConsistOnlyFromSingleLineWhiteSpaces(this string text) => text.ConsistOnlyFromSingleLineWhiteSpaces(0, text.Length);

        public static bool ConsistOnlyFromSingleLineWhiteSpaces(this string text, int start, int length)
        {
            int end = start + length;
            for (int i = start; i < end; i++)
            {
                var ch = text[i];
                if (ch != ' ' && ch != '\t') return false;
            }

            return true;
        }

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

        public static bool StartsWithLineBreak(this string text) => text.StartsWithLineBreak(0, text.Length);

        public static bool StartsWithLineBreak(this string text, int start, int length)
        {
            if (length < 2) return false;
            return text[0] == '\r' && text[1] == '\n';
        }

        public static bool EndsWithLineBreak(this string text) => text.EndsWithLineBreak(0, text.Length);

        public static bool EndsWithLineBreak(this string text, int start, int length)
        {
            if (length < 2) return false;
            return text[text.Length - 2] == '\r' && text[text.Length - 1] == '\n';
        }

        public static IEnumerable<T> Cast<T>(this IEnumerable items)
        {
            foreach (T item in items)
            {
                yield return item;
            }
        }

        public static Span AddToEnd(this Span span, int charactersCount) => new Span(span.Start, span.Length + charactersCount);

        public static Span RemoveFromEnd(this Span span, int charactersCount) => new Span(span.Start, span.Length - charactersCount);

        public static int NumberOfWhiteSpaceCharsOnStartOfLine(this string line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch != ' ' && ch != '\t') return i;
            }
            return 0;
        }
    }
}
