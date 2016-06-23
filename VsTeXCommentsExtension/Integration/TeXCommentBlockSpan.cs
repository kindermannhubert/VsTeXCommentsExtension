using Microsoft.VisualStudio.Text;

namespace VsTeXCommentsExtension.Integration
{
    internal struct TeXCommentBlockSpan
    {
        public Span Span { get; private set; }
        public Span SpanWithLastLineBreak { get; private set; }
        public Span FirstLineSpan { get; private set; }
        public int FirstLineStartWhiteSpaces { get; }
        public string LineBreakText { get; }

        public TeXCommentBlockSpan(Span firstLineSpan, int firstLineStartWhiteSpaces, string lineBreakText)
        {
            FirstLineSpan = firstLineSpan;
            Span = firstLineSpan;
            SpanWithLastLineBreak = firstLineSpan;
            FirstLineStartWhiteSpaces = firstLineStartWhiteSpaces;
            LineBreakText = lineBreakText;
        }

        public void Add(int charactersCount)
        {
            Span = new Span(Span.Start, Span.Length + charactersCount);
            SpanWithLastLineBreak = Span;
        }

        public void RemoveLastLineBreak(int breakLength)
        {
            Span = new Span(Span.Start, Span.Length - breakLength);
        }

        public bool IsPositionAfterTeXPrefix(ITextSnapshot snapshot, int position)
        {
            return position - Span.Start - FirstLineStartWhiteSpaces >= TextSnapshotTeXCommentBlocks.TeXCommentPrefix.Length;
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
