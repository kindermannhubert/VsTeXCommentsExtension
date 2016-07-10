using Microsoft.VisualStudio.Text;

namespace VsTeXCommentsExtension.Integration
{
    public interface ITagSpan
    {
        /// <summary>
        /// Span of whole tag (without last line break).
        /// </summary>
        Span Span { get; }

        /// <summary>
        /// Span of whole tag.
        /// </summary>
        Span SpanWithLastLineBreak { get; }
    }
}
