using System;
using System.Diagnostics;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using VsTeXCommentsExtension.Integration.Data;

namespace VsTeXCommentsExtension.Integration
{
    public readonly struct TeXCommentBlockSpan
    {
        /// <summary>
        /// Span of whole TeX comment block (without last line break).
        /// </summary>
        public readonly Span Span;

        /// <summary>
        /// Span of whole TeX comment block.
        /// </summary>
        public readonly Span SpanWithLastLineBreak;

        /// <summary>
        /// Number of white spaces on first line before '//tex:' prefix.
        /// </summary>
        public readonly int FirstLineWhiteSpacesAtStart;

        /// <summary>
        /// Number of white spaces on last line before '//' prefix.
        /// </summary>
        public readonly int LastLineWhiteSpacesAtStart;

        /// <summary>
        /// Length of properties segment (e.g., "[zoom=120%]")
        /// </summary>
        public readonly int PropertiesSegmentLength;

        /// <summary>
        /// Line break text used (should be "\r\n").
        /// </summary>
        public readonly string LineBreakText;

        public readonly int ZoomPercentage;

        public readonly Color? ForegroundColor;

        public readonly string SyntaxErrors;

        public readonly string CommentPrefix;

        public readonly string TeXCommentPrefix;

        public TeXCommentBlockSpan(
            Span span,
            Span spanWithLastLineBreak,
            int firstLineWhiteSpacesAtStart,
            int lastLineWhiteSpacesAtStart,
            int propertiesSegmentLength,
            string lineBreakText,
            int zoomPercentage,
            Color? foregroundColor,
            string syntaxErrors,
            string documentContentType)
        {
            Span = span;
            SpanWithLastLineBreak = spanWithLastLineBreak;
            FirstLineWhiteSpacesAtStart = firstLineWhiteSpacesAtStart;
            LastLineWhiteSpacesAtStart = lastLineWhiteSpacesAtStart;
            PropertiesSegmentLength = propertiesSegmentLength;
            LineBreakText = lineBreakText;
            ZoomPercentage = zoomPercentage;
            ForegroundColor = foregroundColor;
            SyntaxErrors = syntaxErrors;

            Debug.Assert(TextSnapshotTeXCommentBlocks.CommentPrefixPerContentType.ContainsKey(documentContentType));
            Debug.Assert(TextSnapshotTeXCommentBlocks.TeXCommentPrefixPerContentType.ContainsKey(documentContentType));
            CommentPrefix = TextSnapshotTeXCommentBlocks.CommentPrefixPerContentType[documentContentType];
            TeXCommentPrefix = TextSnapshotTeXCommentBlocks.TeXCommentPrefixPerContentType[documentContentType];
        }

        public TeXCommentTag GetDataTag(ITextSnapshot snapshot) => new TeXCommentTag(snapshot.GetText(Span), this);

        public bool IsPositionAfterTeXPrefix(int position) => position - Span.Start - FirstLineWhiteSpacesAtStart >= TeXCommentPrefix.Length;

        public int GetMinNumberOfWhitespacesBeforeCommentPrefixes(ITextSnapshot snapshot)
        {
            var firstLineIndex = snapshot.GetLineNumberFromPosition(Span.Start);
            var lastLineIndex = snapshot.GetLineNumberFromPosition(Span.End);

            int min = int.MaxValue;
            for (int lineIndex = firstLineIndex; lineIndex <= lastLineIndex; lineIndex++)
            {
                var line = snapshot.GetLineFromLineNumber(lineIndex);
                int whitespaces = line.GetText().NumberOfWhiteSpaceCharsOnStartOfLine();
                if (whitespaces < min) min = whitespaces;
            }

            return min;
        }

        public static int GetMinNumberOfWhitespacesBeforeCommentPrefixes(string teXBlock)
        {
            //TODO perf

            var lines = teXBlock.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            int min = int.MaxValue;
            foreach (var line in lines)
            {
                int whitespaces = line.NumberOfWhiteSpaceCharsOnStartOfLine();
                if (whitespaces < min) min = whitespaces;
            }

            return min;
        }
    }
}
