using Microsoft.VisualStudio.Text;

namespace VsTeXCommentsExtension.Integration
{
    internal struct TeXCommentBlockSpanBuilder
    {
        private readonly string lineBreakText;
        private Span span;
        private int firstLineWhiteSpacesAtStart;

        public TeXCommentBlockSpanBuilder(Span firstLineSpanWithLineBreak, int firstLineWhiteSpacesAtStart, string lineBreakText)
        {
            span = firstLineSpanWithLineBreak;
            this.firstLineWhiteSpacesAtStart = firstLineWhiteSpacesAtStart;
            this.lineBreakText = lineBreakText;
        }

        public void Add(int charactersCount)
        {
            span = span.AddToEnd(charactersCount);
        }

        public void RemoveLastLineBreak(int breakLength)
        {
            span = span.RemoveFromEnd(breakLength);
        }

        public TeXCommentBlockSpan Build(ITextSnapshot snapshot)
        {
            var spanWithLastLineBreak = span.AddToEnd(lineBreakText.Length);
            if (spanWithLastLineBreak.Start + spanWithLastLineBreak.Length >= snapshot.Length ||
                snapshot.GetText(spanWithLastLineBreak.Start - lineBreakText.Length, lineBreakText.Length) != lineBreakText)
            {
                //there is no line break at the end of block
                spanWithLastLineBreak = span;
            }
            return new TeXCommentBlockSpan(span, spanWithLastLineBreak, firstLineWhiteSpacesAtStart, lineBreakText);
        }
    }
}
