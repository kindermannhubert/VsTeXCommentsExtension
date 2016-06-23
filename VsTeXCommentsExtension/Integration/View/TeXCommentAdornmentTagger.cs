using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.View;

namespace VsTeXCommentsExtension.Integration.View
{
    internal sealed class TeXCommentAdornmentTagger : IntraTextAdornmentTagTransformer<TeXCommentTag, TeXCommentAdornment>
    {
        private readonly IRenderingManager renderingManager;
        private readonly List<TeXCommentAdornment> linesWithAdornments = new List<TeXCommentAdornment>();

        public SolidColorBrush CommentsForegroundBrush { get; set; }

        internal static TeXCommentAdornmentTagger GetTagger(
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
            CommentsForegroundBrush = commentsForegroundBrush;
            view.TextBuffer.Changed += TextBuffer_Changed;

            VisualStudioSettings.Instance.CommentsColorChanged += ColorsChanged;
            VisualStudioSettings.Instance.ZoomChanged += ZoomChanged;
            ExtensionSettings.Instance.CustomZoomChanged += CustomZoomChanged;
        }

        private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            HandleAutoCommentPrefixInsertionAfterEdit(e);
            HandleSwitchingToEditModeAfterEdit(e);
        }

        private void HandleAutoCommentPrefixInsertionAfterEdit(TextContentChangedEventArgs e)
        {
            //if we put new line inside TeX-block we want to automaticaly insert '//'
            if (e.Changes.Count != 1) return;

            var change = e.Changes[0];
            if (change.NewText != "\r\n") return;

            var block = texCommentBlocks.GetBlockForPosition(e.Before, change.OldPosition);
            if (!block.HasValue) return;

            if (!block.Value.IsPositionAfterTeXPrefix(e.Before, change.OldPosition)) return;

            var line = e.Before.GetLineFromPosition(change.OldPosition);
            var whitespaceCount = block.Value.GetMinNumberOfWhitespacesBeforeCommentPrefixes(e.Before);

            textView.TextBuffer.Insert(e.Changes[0].NewEnd, new string(' ', whitespaceCount) + "//");
        }

        private void HandleSwitchingToEditModeAfterEdit(TextContentChangedEventArgs e)
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

        private DateTime lastTimeZoomChanged;
        private void ZoomChanged(IWpfTextView textView, double zoomPercentage)
        {
            //Zoom is changing continuously (while changing by mouse wheel).
            //We want to wait a moment before triggering invalidation (we hope that after moment changing is done).
            const int delayMs = 1000;
            var now = DateTime.Now;
            if ((now - lastTimeZoomChanged).TotalMilliseconds > delayMs)
            {
                Task.Run(
                    () =>
                    {
                        while ((DateTime.Now - lastTimeZoomChanged).TotalMilliseconds < delayMs)
                        {
                            Thread.Sleep(delayMs / 10);
                        }
                        lastTimeZoomChanged = DateTime.Now;
                        textView.VisualElement.Dispatcher.BeginInvoke(new Action(() => ForAllCurrentlyUsedAdornments(a => a.Invalidate(), true)));
                    });
            }

            lastTimeZoomChanged = now;
        }

        private void CustomZoomChanged(double zoomScale)
        {
            ForAllCurrentlyUsedAdornments(a => a.Invalidate(), true);
        }

        private void ColorsChanged(IWpfTextView textView, SolidColorBrush foreground, SolidColorBrush background)
        {
            CommentsForegroundBrush = foreground;
            ForAllCurrentlyUsedAdornments(a => a.Invalidate(), true);
        }

        public override void Dispose()
        {
            base.Dispose();
            textView.TextBuffer.Changed -= TextBuffer_Changed;
            VisualStudioSettings.Instance.ZoomChanged -= ZoomChanged;
            VisualStudioSettings.Instance.CommentsColorChanged -= ColorsChanged;
            ExtensionSettings.Instance.CustomZoomChanged -= CustomZoomChanged;
            textView.Properties.RemoveProperty(typeof(TeXCommentAdornmentTagger));
        }

        protected override TeXCommentAdornment CreateAdornment(TeXCommentTag dataTag, Span adornmentSpan, IntraTextAdornmentTaggerDisplayMode defaultDisplayMode)
        {
            var firstLine = Snapshot.GetLineNumberFromPosition(dataTag.Span.Start);
            var lastLine = Snapshot.GetLineNumberFromPosition(dataTag.Span.End);
            var lineSpan = new LineSpan(firstLine, lastLine);

            var adornment = new TeXCommentAdornment(
                dataTag,
                lineSpan,
                CommentsForegroundBrush,
                defaultDisplayMode,
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
                isInEditMode =>
                {
                    ForAllCurrentlyUsedAdornments(a => a.IsInEditMode = isInEditMode, false);
                },
                renderingManager,
                textView);
            textView.TextBuffer.Changed += adornment.HandleTextBufferChanged;

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

            adornment.CommentsForegroundBrush = CommentsForegroundBrush;
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
