using Microsoft.VisualStudio.Text;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Media;

namespace VsTeXCommentsExtension.Integration
{
    internal struct TeXCommentBlockSpanBuilder
    {
        private static readonly Regex PropertiesSegmentsRegex = new Regex("^[ \t]*" + TextSnapshotTeXCommentBlocks.TeXCommentPrefix + @"(\[[a-zA-Z]+=[a-zA-Z0-9#%]+\])+", RegexOptions.Compiled);
        private static readonly Regex PropertyRegex = new Regex(@"(\[(?<name>[a-zA-Z]+)=(?<value>[a-zA-Z0-9#%]+)\])+", RegexOptions.Compiled);

        private readonly string lineBreakText;
        private readonly int firstLineWhiteSpacesAtStart;
        private readonly int propertiesSegmentLength;
        private readonly int zoomPercentage;
        private readonly Color? foregroundColor;
        private readonly string syntaxErrors;

        private Span span;
        private int lastLineWhiteSpacesAtStart;

        public TeXCommentBlockSpanBuilder(Span firstLineSpanWithLineBreak, int firstLineWhiteSpacesAtStart, string firstLineText, string lineBreakText)
        {
            span = firstLineSpanWithLineBreak;
            this.firstLineWhiteSpacesAtStart = firstLineWhiteSpacesAtStart;
            this.lineBreakText = lineBreakText;
            lastLineWhiteSpacesAtStart = -1;
            zoomPercentage = 100;
            propertiesSegmentLength = 0;
            foregroundColor = null;
            syntaxErrors = null;

            //search for properties (e.g., //tex:[zoom=120%])
            int propertiesIndex = firstLineWhiteSpacesAtStart + TextSnapshotTeXCommentBlocks.TeXCommentPrefix.Length;
            if (propertiesIndex < firstLineText.Length && firstLineText[propertiesIndex] == '[')
            {
                var match = PropertiesSegmentsRegex.Match(firstLineText);
                if (match.Success)
                {
                    var propertiesSegmentGroup = match.Groups[1];
                    foreach (Capture prop in propertiesSegmentGroup.Captures)
                    {
                        propertiesSegmentLength += prop.Value.Length;
                        match = PropertyRegex.Match(prop.Value);
                        if (match.Success)
                        {
                            var valueText = match.Groups["value"].Value;
                            switch (match.Groups["name"].Value)
                            {
                                case "zoom":
                                    if (valueText[valueText.Length - 1] != '%')
                                    {
                                        syntaxErrors = "Unable to parse value of 'zoom' property. Example of syntax is: '//tex:[zoom=120%]'.";
                                        return;
                                    }

                                    int zoom;
                                    if (int.TryParse(valueText.Substring(0, valueText.Length - 1), out zoom))
                                    {
                                        zoomPercentage = zoom;
                                    }
                                    else
                                    {
                                        syntaxErrors = "Unable to parse value of 'zoom' property. Example of syntax is: '//tex:[zoom=120%]'.";
                                        return;
                                    }
                                    break;
                                case "foreground":
                                    foregroundColor = ParseColorFromString(valueText);
                                    if (!foregroundColor.HasValue)
                                    {
                                        syntaxErrors = "Unable to parse value of 'foreground' property. Examples of syntax are: '//tex:[foreground=red]' or '//tex:[foreground=#FF0000]'.";
                                        return;
                                    }
                                    break;
                                default:
                                    syntaxErrors = $"Unknown property name '{match.Groups["name"].Value}' used.";
                                    return;
                            }
                        }
                        else
                        {
                            syntaxErrors = "Unable to parse properties of TeX comment block.";
                            return;
                        }
                    }
                }
            }
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
            return new TeXCommentBlockSpan(
                span,
                spanWithLastLineBreak,
                firstLineWhiteSpacesAtStart,
                lastLineWhiteSpacesAtStart,
                propertiesSegmentLength,
                lineBreakText,
                zoomPercentage,
                foregroundColor,
                syntaxErrors);
        }

        [DebuggerNonUserCode]
        private static Color? ParseColorFromString(string text)
        {
            try
            {
                return (Color?)ColorConverter.ConvertFromString(text);
            }
            catch
            {
                return null;
            }
        }
    }
}
