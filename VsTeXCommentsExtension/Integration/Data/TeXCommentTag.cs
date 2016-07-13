using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Diagnostics;
using System.Text;

namespace VsTeXCommentsExtension.Integration.Data
{
    /// <summary>
    /// Data tag indicating that the tagged text represents a TeX comment block.
    /// </summary>
    /// <remarks>
    /// Note that this tag has nothing directly to do with adornments or other UI.
    /// This sample's adornments will be produced based on the data provided in these tags.
    /// This separation provides the potential for other extensions to consume tags
    /// and provide alternative UI or other derived functionality over this data.
    /// </remarks>
    public struct TeXCommentTag : ITag, ITagSpan
    {
        private string textTrimmed;
        public readonly string Text;
        public readonly TeXCommentBlockSpan TeXBlock;

        /// <summary>
        /// Span of whole TeX comment block (without last line break).
        /// </summary>
        public Span Span => TeXBlock.Span;

        /// <summary>
        /// Span of whole TeX comment block.
        /// </summary>
        public Span SpanWithLastLineBreak => TeXBlock.SpanWithLastLineBreak;

        public TeXCommentTag(string text, TeXCommentBlockSpan span)
        {
            Debug.Assert(text != null);

            textTrimmed = null;
            Text = text.TrimStart(TextSnapshotTeXCommentBlocks.WhiteSpaces);
            TeXBlock = span;
        }

        public string GetTextWithoutCommentMarks()
        {
            if (textTrimmed == null)
            {
                //TODO perf and allocations

                var sb = new StringBuilder(Text.Length);
                foreach (var line in Text.Split(new[] { TeXBlock.LineBreakText }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmedLine = line.TrimStart(TextSnapshotTeXCommentBlocks.WhiteSpaces);
                    if (trimmedLine.StartsWith(TextSnapshotTeXCommentBlocks.TeXCommentPrefix))
                    {
                        trimmedLine = trimmedLine.Substring(TextSnapshotTeXCommentBlocks.TeXCommentPrefix.Length);
                    }
                    else if (trimmedLine.StartsWith(TextSnapshotTeXCommentBlocks.CommentPrefix))
                    {
                        trimmedLine = trimmedLine.Substring(TextSnapshotTeXCommentBlocks.CommentPrefix.Length);
                    }

                    sb.AppendLine(trimmedLine);
                }

                textTrimmed = sb.ToString();
            }

            return textTrimmed;
        }
    }
}
