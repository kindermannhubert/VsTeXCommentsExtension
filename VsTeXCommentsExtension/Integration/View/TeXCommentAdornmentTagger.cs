using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.View;

namespace VsTeXCommentsExtension.Integration.View
{
    /// <summary>
    /// Provides color swatch adornments in place of color constants.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a sample usage of the <see cref="IntraTextAdornmentTagTransformer"/> utility class.
    /// </para>
    /// </remarks>
    internal sealed class TeXCommentAdornmentTagger
        : IntraTextAdornmentTagTransformer<TeXCommentTag, TeXCommentAdornment>
    {
        private readonly IRenderingManager renderingManager;
        private readonly SolidColorBrush commentsForegroundBrush;
        private readonly List<TeXCommentAdornment> linesWithAdornments = new List<TeXCommentAdornment>();

        internal static ITagger<IntraTextAdornmentTag> GetTagger(
            IWpfTextView view,
            Lazy<ITagAggregator<TeXCommentTag>> texCommentTagger,
            IRenderingManager renderingManager,
            SolidColorBrush commentsForegroundBrush)
        {
            return view.Properties.GetOrCreateSingletonProperty(
                () => new TeXCommentAdornmentTagger(view, renderingManager, texCommentTagger.Value, commentsForegroundBrush));
        }

        private TeXCommentAdornmentTagger(
            IWpfTextView view,
            IRenderingManager renderingManager,
            ITagAggregator<TeXCommentTag> texCommentTagger,
            SolidColorBrush commentsForegroundBrush)
            : base(view, texCommentTagger, IntraTextAdornmentTaggerDisplayMode.HideOriginalText)
        {
            this.renderingManager = renderingManager;
            this.commentsForegroundBrush = commentsForegroundBrush;
            view.TextBuffer.Changed += TextBuffer_Changed;
        }

        private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            //when we start editing line with adornment we switch to edit mode
            foreach (var change in e.Changes)
            {
                //white space changes are treated specialy (they don't have to trigger switch to edit mode)

                //switch to edit mode can happen only for adornment on first change line and change must be on the end of line (next lines will be over or before other adornment thus we are not interested in them)
                var firstLineOld = e.Before.GetLineFromPosition(change.OldPosition);
                if (change.OldPosition == firstLineOld.End)
                {
                    var firstLineNew = e.After.GetLineFromPosition(change.NewPosition);
                    if (firstLineOld.LineNumber == firstLineNew.LineNumber) //should be most of the cases
                    {
                        var firstLineOldText = firstLineOld.GetTextIncludingLineBreak();
                        var firstLineNewText = firstLineNew.GetTextIncludingLineBreak();

                        var firstLineOldChangeStart = change.OldPosition - firstLineOld.Start;
                        var firstLineNewChangeStart = change.NewPosition - firstLineNew.Start;

                        //when we insert or delete new line after adornment, we do not want to switch to edit mode
                        if (!firstLineOldText.ConsistOnlyFromLineBreaks(firstLineOldChangeStart, Math.Min(firstLineOldText.Length - firstLineOldChangeStart, change.OldLength)) ||
                            !firstLineNewText.ConsistOnlyFromLineBreaks(firstLineNewChangeStart, Math.Min(firstLineNewText.Length - firstLineNewChangeStart, change.NewLength)))
                        {
                            var adornmentOnLine = GetAdornmentOnLine(firstLineOld.LineNumber);
                            if (adornmentOnLine != null && !adornmentOnLine.IsInEditMode)
                            {
                                adornmentOnLine.IsInEditMode = true;
                            }
                        }
                    }
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            view.Properties.RemoveProperty(typeof(TeXCommentAdornmentTagger));
        }

        protected override TeXCommentAdornment CreateAdornment(TeXCommentTag dataTag, Span adornmentSpan, IntraTextAdornmentTaggerDisplayMode defaultDisplayMode)
        {
            var firstLine = Snapshot.GetLineNumberFromPosition(dataTag.Span.Start);
            var lastLine = Snapshot.GetLineNumberFromPosition(dataTag.Span.End);
            var lineSpan = new LineSpan(firstLine, lastLine);

            var adornment = new TeXCommentAdornment(
                dataTag,
                commentsForegroundBrush,
                renderingManager,
                lineSpan,
                span =>
                {
                    //var blockSpans = texCommentBlocks.GetBlockSpansWithLastLineBreakIntersectedBy(Snapshot, span);
                    //foreach (var blockSpan in blockSpans)
                    //{
                    //    RaiseTagsChanged(new SnapshotSpan(Snapshot, blockSpan));
                    //}

                    //RaiseTagsChanged(new SnapshotSpan(Snapshot, 0, Snapshot.Length));

                    InvalidateSpans(new List<SnapshotSpan>() { new SnapshotSpan(Snapshot, 0, Snapshot.Length) });
                },
                defaultDisplayMode);
            view.TextBuffer.Changed += adornment.HandleTextBufferChanged;

            MarkAdornmentLines(lineSpan, adornment);

            return adornment;
        }

        protected override void UpdateAdornment(TeXCommentAdornment adornment, TeXCommentTag dataTag, Span adornmentSpan)
        {
            var firstLine = Snapshot.GetLineNumberFromPosition(dataTag.Span.Start);
            var lastLine = Snapshot.GetLineNumberFromPosition(dataTag.Span.End);
            var lineSpan = new LineSpan(firstLine, lastLine);

            MarkAdornmentLines(adornment.LineSpan, null); //remove old
            MarkAdornmentLines(lineSpan, adornment); //add new

            adornment.Update(dataTag, lineSpan);
        }

        private void MarkAdornmentLines(LineSpan lineSpan, TeXCommentAdornment adornment)
        {
            lock (linesWithAdornments)
            {
                if (lineSpan.LastLine >= linesWithAdornments.Count)
                {
                    int newItemsCount = lineSpan.LastLine - linesWithAdornments.Count + 1;
                    for (int i = 0; i < newItemsCount; i++)
                    {
                        linesWithAdornments.Add(null);
                    }
                }

                for (int i = lineSpan.FirstLine; i <= lineSpan.LastLine; i++)
                {
                    linesWithAdornments[i] = adornment;
                }
            }
        }

        private TeXCommentAdornment GetAdornmentOnLine(int line)
        {
            if (line >= linesWithAdornments.Count) return null;
            return linesWithAdornments[line];
        }
    }
}
