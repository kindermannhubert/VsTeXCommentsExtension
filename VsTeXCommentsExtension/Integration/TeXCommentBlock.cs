using Microsoft.VisualStudio.Text;

namespace VsTeXCommentsExtension.Integration
{
    internal struct TeXCommentBlock
    {
        public Span Span { get; private set; }
        public Span SpanWithLastLineBreak { get; private set; }
        public Span FirstLineSpan { get; private set; }
        public int FirstLineStartWhiteSpaces { get; }
        public string LineBreakText { get; }

        public TeXCommentBlock(Span firstLineSpan, int firstLineStartWhiteSpaces, string lineBreakText)
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
    }
}
