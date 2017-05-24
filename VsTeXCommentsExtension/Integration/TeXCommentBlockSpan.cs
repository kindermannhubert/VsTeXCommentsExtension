using Microsoft.VisualStudio.Text;
using System;
using System.Windows.Media;
using VsTeXCommentsExtension.Integration.Data;

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
        /// Number of white spaces on first line before '//tex:' prefix.
        /// </summary>
        public int FirstLineWhiteSpacesAtStart { get; }

        /// <summary>
        /// Number of white spaces on last line before '//' prefix.
        /// </summary>
        public int LastLineWhiteSpacesAtStart { get; }

        /// <summary>
        /// Length of properties segment (e.g., "[zoom=120%]")
        /// </summary>
        public int PropertiesSegmentLength { get; }

        /// <summary>
        /// Line break text used (should be "\r\n").
        /// </summary>
        public string LineBreakText { get; }

        public int ZoomPercentage { get; }

        public Color? ForegroundColor { get; }

        public string SyntaxErrors { get; }

        public TeXCommentBlockSpan(
            Span span,
            Span spanWithLastLineBreak,
            int firstLineWhiteSpacesAtStart,
            int lastLineWhiteSpacesAtStart,
            int propertiesSegmentLength,
            string lineBreakText,
            int zoomPercentage,
            Color? foregroundColor,
            string syntaxErrors)
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
        }

        public TeXCommentTag GetDataTag(ITextSnapshot snapshot) => new TeXCommentTag(snapshot.GetText(Span), this);

        public bool IsPositionAfterTeXPrefix(ITextSnapshot snapshot, int position)
        {
            if (snapshot.ContentType.TypeName == "CSharp")
            {
                return position - Span.Start - FirstLineWhiteSpacesAtStart >= TextSnapshotTeXCommentBlocks.TeXCommentPrefixCSharpAndFSharpAndCpp.Length;
            }
            return position - Span.Start - FirstLineWhiteSpacesAtStart >= TextSnapshotTeXCommentBlocks.TeXCommentPrefixBasic.Length;
        }

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
