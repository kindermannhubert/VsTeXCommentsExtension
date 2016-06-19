using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Diagnostics;
using System.Text;

namespace VsTeXCommentsExtension.Integration.Data
{
    /// <summary>
    /// Data tag indicating that the tagged text represents a color.
    /// </summary>
    /// <remarks>
    /// Note that this tag has nothing directly to do with adornments or other UI.
    /// This sample's adornments will be produced based on the data provided in these tags.
    /// This separation provides the potential for other extensions to consume color tags
    /// and provide alternative UI or other derived functionality over this data.
    /// </remarks>
    public struct TeXCommentTag : ITag
    {
        private readonly string lineBreakText;
        private string textTrimmed;
        public readonly string Text;
        public readonly Span Span;

        public TeXCommentTag(string text, string lineBreakText, Span span)
        {
            Debug.Assert(text != null);
            Debug.Assert(lineBreakText != null);

            this.lineBreakText = lineBreakText;
            textTrimmed = null;
            Text = text.TrimStart(TextSnapshotTeXCommentBlocks.WhiteSpaces);
            Span = span;
        }

        public string GetTextWithoutCommentMarks()
        {
            if (textTrimmed == null)
            {
                //TODO perf and allocations

                var sb = new StringBuilder(Text.Length);
                foreach (var line in Text.Split(new[] { lineBreakText }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmedLine = line.TrimStart(TextSnapshotTeXCommentBlocks.WhiteSpaces);
                    if (trimmedLine.StartsWith(TextSnapshotTeXCommentBlocks.TexCommentPrefix))
                    {
                        trimmedLine = trimmedLine.Substring(TextSnapshotTeXCommentBlocks.TexCommentPrefix.Length);
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
