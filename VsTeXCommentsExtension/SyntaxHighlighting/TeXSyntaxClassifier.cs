using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using VsTeXCommentsExtension.Integration;

namespace VsTeXCommentsExtension.SyntaxHighlighting
{
    public class TeXSyntaxClassifier : IClassifier, IDisposable
    {
        public static readonly Regex MathBlockRegex = new Regex(@"([\$]?\$)[^\$]+\$[\$]?", RegexOptions.Multiline | RegexOptions.Compiled);
        public static readonly Regex CommandRegex = new Regex(@"\\[^ {}_\^\$\r\n]+", RegexOptions.Multiline | RegexOptions.Compiled);
        public static readonly Regex TexPrefixRegexCSharp = new Regex($@"^[ \t]*({TextSnapshotTeXCommentBlocks.TeXCommentPrefixCSharpAndFSharpAndCpp})", RegexOptions.Compiled);
        public static readonly Regex TexPrefixRegexBasic = new Regex($@"^[ \t]*({TextSnapshotTeXCommentBlocks.TeXCommentPrefixBasic})", RegexOptions.Compiled);

        private readonly ITextBuffer buffer;
        private readonly TextSnapshotTeXCommentBlocks texCommentBlocks;
        private readonly IClassificationTypeRegistryService classificationTypeRegistry;
        private readonly IClassificationType commandClassificationType;
        private readonly IClassificationType mathBlockClassificationType;

        private bool isDisposed;

        internal TeXSyntaxClassifier(ITextBuffer buffer, IClassificationTypeRegistryService registry)
        {
            this.buffer = buffer;
            this.classificationTypeRegistry = registry;
            texCommentBlocks = TextSnapshotTeXCommentBlocksProvider.Get(buffer);

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
            using (var blocks = texCommentBlocks.GetBlocksIntersectedBy(span.Snapshot, span.Span))
            {
                foreach (var block in blocks)
                {
                    var blockText = snapshot.GetText(block.Span);

                    foreach (Match mathBlockMatch in MathBlockRegex.Matches(blockText))
                    {
                        //commands colorizing (="\someCommand")
                        foreach (Match commandMatch in CommandRegex.Matches(mathBlockMatch.Value))
                        {
                            var commandSpan = new Span(block.Span.Start + mathBlockMatch.Index + commandMatch.Index, commandMatch.Length);
                            spans.Add(new ClassificationSpan(new SnapshotSpan(snapshot, commandSpan), commandClassificationType));
                        }

                        //math block borders colorizing (="$" or "$$")
                        var dollarStartIndex = mathBlockMatch.Index;
                        var doubleDollar = dollarStartIndex + 1 < blockText.Length && blockText[dollarStartIndex + 1] == '$';
                        var dollarSpan = new Span(block.Span.Start + dollarStartIndex, doubleDollar ? 2 : 1);
                        spans.Add(new ClassificationSpan(new SnapshotSpan(snapshot, dollarSpan), mathBlockClassificationType));

                        dollarStartIndex = mathBlockMatch.Index + mathBlockMatch.Length - 1;
                        doubleDollar = dollarStartIndex - 1 >= 0 && blockText[dollarStartIndex - 1] == '$';
                        dollarSpan = new Span(block.Span.Start + (doubleDollar ? dollarStartIndex - 1 : dollarStartIndex), doubleDollar ? 2 : 1);
                        spans.Add(new ClassificationSpan(new SnapshotSpan(snapshot, dollarSpan), mathBlockClassificationType));
                    }

                    //colorization of "tex:" prefix
                    var prefixMatch = TexPrefixRegexCSharp.Match(blockText);
                    var prefixStart = prefixMatch.Groups[1].Index + TextSnapshotTeXCommentBlocks.CommentPrefixCSharp.Length;
                    var prefixLength = TextSnapshotTeXCommentBlocks.TeXCommentPrefixCSharpAndFSharpAndCpp.Length - TextSnapshotTeXCommentBlocks.CommentPrefixCSharp.Length;

                    if (snapshot.ContentType.TypeName == "Basic")
                    {
                        prefixMatch = TexPrefixRegexBasic.Match(blockText);
                        prefixStart = prefixMatch.Groups[1].Index + TextSnapshotTeXCommentBlocks.CommentPrefixBasic.Length;
                        prefixLength = TextSnapshotTeXCommentBlocks.TeXCommentPrefixBasic.Length - TextSnapshotTeXCommentBlocks.CommentPrefixBasic.Length;
                    }
                    Debug.Assert(prefixMatch.Success && prefixMatch.Groups.Count == 2);
                    var texPrefixSpan = new Span(block.Span.Start + prefixStart, prefixLength);
                    spans.Add(new ClassificationSpan(new SnapshotSpan(snapshot, texPrefixSpan), mathBlockClassificationType));

                    //colorization of property attributes
                    if (block.PropertiesSegmentLength > 0)
                    {
                        spans.Add(new ClassificationSpan(new SnapshotSpan(snapshot, new Span(texPrefixSpan.End, block.PropertiesSegmentLength)), mathBlockClassificationType));
                    }
                }
            }

            return spans;
        }

        public void Dispose()
        {
            if (isDisposed) return;

            try
            {
                TextSnapshotTeXCommentBlocksProvider.Release(buffer, texCommentBlocks);
            }
            finally
            {
                isDisposed = true;
            }
        }
    }
}
