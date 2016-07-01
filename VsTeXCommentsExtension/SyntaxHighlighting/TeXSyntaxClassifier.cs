using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using VsTeXCommentsExtension.Integration;

namespace VsTeXCommentsExtension.SyntaxHighlighting
{
    public class TeXSyntaxClassifier : IClassifier
    {
        private readonly TextSnapshotTeXCommentBlocks texCommentBlocks = new TextSnapshotTeXCommentBlocks();
        private readonly IClassificationTypeRegistryService classificationTypeRegistry;
        private readonly Regex mathBlockRegex;
        private readonly Regex commandRegex;
        private readonly Regex texPrefixRegex;
        private readonly IClassificationType commandClassificationType;
        private readonly IClassificationType mathBlockClassificationType;

        internal TeXSyntaxClassifier(IClassificationTypeRegistryService registry)
        {
            this.classificationTypeRegistry = registry;

            mathBlockRegex = new Regex(@"[\$]?\$[^\$]+\$[\$]?", RegexOptions.Multiline | RegexOptions.Compiled);
            commandRegex = new Regex(@"\\[^ {}_\^\$]+", RegexOptions.Multiline | RegexOptions.Compiled);
            texPrefixRegex = new Regex($@"^[ \t]*({TextSnapshotTeXCommentBlocks.TeXCommentPrefix})", RegexOptions.Compiled);

            commandClassificationType = classificationTypeRegistry.GetClassificationType("TeX.command");
            mathBlockClassificationType = classificationTypeRegistry.GetClassificationType("TeX.mathBlock");
        }

#pragma warning disable 67
        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;
#pragma warning restore 67

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var snapshot = span.Snapshot;
            var spans = new List<ClassificationSpan>();
            var blocks = texCommentBlocks.GetBlockSpansIntersectedBy(span.Snapshot, span.Span);
            foreach (var blockSpan in blocks)
            {
                var blockText = blockSpan.GetText();

                foreach (Match mathBlockMatch in mathBlockRegex.Matches(blockText))
                {
                    //commands colorizing (="\someCommand")
                    foreach (Match commandMatch in commandRegex.Matches(mathBlockMatch.Value))
                    {
                        var commandSpan = new Span(blockSpan.Start + mathBlockMatch.Index + commandMatch.Index, commandMatch.Length);
                        spans.Add(new ClassificationSpan(new SnapshotSpan(snapshot, commandSpan), commandClassificationType));
                    }

                    //math block borders colorizing (="$" or "$$")
                    var dollarStartIndex = mathBlockMatch.Index;
                    var doubleDollar = dollarStartIndex + 1 < blockText.Length && blockText[dollarStartIndex + 1] == '$';
                    var dollarSpan = new Span(blockSpan.Start + dollarStartIndex, doubleDollar ? 2 : 1);
                    spans.Add(new ClassificationSpan(new SnapshotSpan(snapshot, dollarSpan), mathBlockClassificationType));

                    dollarStartIndex = mathBlockMatch.Index + mathBlockMatch.Length - 1;
                    doubleDollar = dollarStartIndex - 1 >= 0 && blockText[dollarStartIndex - 1] == '$';
                    dollarSpan = new Span(blockSpan.Start + (doubleDollar ? dollarStartIndex - 1 : dollarStartIndex), doubleDollar ? 2 : 1);
                    spans.Add(new ClassificationSpan(new SnapshotSpan(snapshot, dollarSpan), mathBlockClassificationType));
                }

                //"tex:" prefix will be colorized too
                var prefixMatch = texPrefixRegex.Match(blockText);
                Debug.Assert(prefixMatch.Success && prefixMatch.Groups.Count == 2);
                var prefixStart = prefixMatch.Groups[1].Index + TextSnapshotTeXCommentBlocks.CommentPrefix.Length;
                var prefixLength = TextSnapshotTeXCommentBlocks.TeXCommentPrefix.Length - TextSnapshotTeXCommentBlocks.CommentPrefix.Length;
                spans.Add(new ClassificationSpan(new SnapshotSpan(snapshot, new Span(blockSpan.Start + prefixStart, prefixLength)), mathBlockClassificationType));
            }

            return spans;
        }
    }
}
