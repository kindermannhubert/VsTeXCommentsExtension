using Microsoft.VisualStudio.Text;

namespace VsTeXCommentsExtension.Integration
{
    internal struct TeXCommentBlockSpanBuilder
    {
        private readonly string lineBreakText;
        private Span span;
        private int firstLineWhiteSpacesAtStart;
        private int lastLineWhiteSpacesAtStart;

        public TeXCommentBlockSpanBuilder(Span firstLineSpanWithLineBreak, int firstLineWhiteSpacesAtStart, string lineBreakText)
        {
            span = firstLineSpanWithLineBreak;
            this.firstLineWhiteSpacesAtStart = firstLineWhiteSpacesAtStart;
            this.lineBreakText = lineBreakText;
            lastLineWhiteSpacesAtStart = -1;
        }

        public void Add(int charactersCount)
        {
            span = span.TranslateEnd(charactersCount);
        }

        public void EndBlock(ITextSnapshotLine lastBlockLine)
        {
            span = span.TranslateEnd(-lastBlockLine.LineBreakLength);
            lastLineWhiteSpacesAtStart = lastBlockLine.GetText().NumberOfWhiteSpaceCharsOnStartOfLine();
        }

        public TeXCommentBlockSpan Build(ITextSnapshot snapshot)
        {
            var spanWithLastLineBreak = span.TranslateEnd(lineBreakText.Length);
            if (span.Start + spanWithLastLineBreak.Length >= snapshot.Length ||
                snapshot.GetText(span.End, lineBreakText.Length) != lineBreakText)
            {
                //there is no line break at the end of block
                spanWithLastLineBreak = span;
            }
            return new TeXCommentBlockSpan(span, spanWithLastLineBreak, firstLineWhiteSpacesAtStart, lastLineWhiteSpacesAtStart, lineBreakText);
        }
    }
}
