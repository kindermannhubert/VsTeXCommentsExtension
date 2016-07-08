using Microsoft.VisualStudio.Text;

namespace VsTeXCommentsExtension.Integration
{
    public struct TeXCommentBlockSpan
    {
        /// <summary>
        /// Span of whole TeX comment block (without last line break).
        /// </summary>
        public Span Span { get; }

        /// <summary>
        /// Span of whole TeX comment block.
        /// </summary>
        public Span SpanWithLastLineBreak { get; }

        /// <summary>
        /// Number of white spaces before on first line before '//tex:' prefix.
        /// </summary>
        public int FirstLineWhiteSpacesAtStart { get; }

        /// <summary>
        /// Line break text used (should be "/r/n").
        /// </summary>
        public string LineBreakText { get; }

        public TeXCommentBlockSpan(Span span, Span spanWithLastLineBreak, int firstLineWhiteSpacesAtStart, string lineBreakText)
        {
            Span = span;
            SpanWithLastLineBreak = spanWithLastLineBreak;
            FirstLineWhiteSpacesAtStart = firstLineWhiteSpacesAtStart;
            LineBreakText = lineBreakText;
        }

        public bool IsPositionAfterTeXPrefix(ITextSnapshot snapshot, int position)
        {
            return position - Span.Start - FirstLineWhiteSpacesAtStart >= TextSnapshotTeXCommentBlocks.TeXCommentPrefix.Length;
        }

        public int GetMinNumberOfWhitespacesBeforeCommentPrefixes(ITextSnapshot snapshot)
        {
            var firstLineIndex = snapshot.GetLineNumberFromPosition(Span.Start);
            var lastLineIndex = snapshot.GetLineNumberFromPosition(Span.End);

            int min = int.MaxValue;
            for (int lineIndex = firstLineIndex; lineIndex <= lastLineIndex; lineIndex++)
            {
                var line = snapshot.GetLineFromLineNumber(lineIndex);
                int whitespaces = NumberOfWhiteSpaceCharsOnStartOfLine(line.GetText());
                if (whitespaces < min) min = whitespaces;
            }

            return min;
        }

        private int NumberOfWhiteSpaceCharsOnStartOfLine(string line)
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
