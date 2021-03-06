using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.View;

namespace VsTeXCommentsExtension.Integration.View
{
    internal sealed class TeXCommentAdornmentTagger : IntraTextAdornmentTagTransformer<TeXCommentTag, TeXCommentAdornment>
    {
        private readonly IRenderingManager renderingManager;
        private readonly List<TeXCommentAdornment> linesWithAdornments = new List<TeXCommentAdornment>();
        private readonly VsSettings vsSettings;
        private bool textHasBeenEdited;
        private bool isDisposed;

        public TeXCommentAdornmentTagger(
            IWpfTextView textView,
            IRenderingManager renderingManager,
            ITagAggregator<TeXCommentTag> texCommentTagger)
            : base(textView, texCommentTagger, IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText_BeforeLastLineBreak)
        {
            this.renderingManager = renderingManager;
            textView.TextBuffer.Changed += TextBuffer_Changed;

            vsSettings = VsSettings.GetOrCreate(textView);
            vsSettings.CommentsColorChanged += ColorsChanged;
            vsSettings.ZoomChanged += ZoomChanged;
            ExtensionSettings.Instance.CustomZoomChanged += CustomZoomChanged;
        }

        private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            textHasBeenEdited = true;
            HandleAutoCommentPrefixInsertionAfterEdit(e);
            HandleSwitchingToEditModeAfterEdit(e);
        }

        private void HandleAutoCommentPrefixInsertionAfterEdit(TextContentChangedEventArgs e)
        {
            //if we put new line inside TeX-block we want to automaticaly insert '//'
            if (e.Changes.Count != 1) return;

            var change = e.Changes[0];
            if (change.NewText != "\r\n") return;
            if (change.OldText.Length != 0) return; //we handle only simple cases

            var block = TexCommentBlocks.GetBlockForPosition(e.Before, change.OldPosition);
            if (!block.HasValue) return;

            if (!block.Value.IsPositionAfterTeXPrefix(change.OldPosition)) return;

            var line = e.Before.GetLineFromPosition(change.OldPosition);
            var oldLinePartWhichIsMovedToNewLine = e.Before.GetText(change.OldPosition, line.Extent.End.Position - change.OldPosition);
            if (oldLinePartWhichIsMovedToNewLine
                .TrimStart(TextSnapshotTeXCommentBlocks.WhiteSpaces)
                .StartsWith(block.Value.CommentPrefix))
            {
                //Enter has been pressed before '//'
                TextView.TextBuffer.Insert(change.NewPosition, block.Value.CommentPrefix); //whitespaces are inserted automatically by VS
            }
            else
            {
                //Enter has been pressed after '//'
                var whitespaceCount = block.Value.GetMinNumberOfWhitespacesBeforeCommentPrefixes(e.Before);
                TextView.TextBuffer.Insert(change.NewEnd, new string(' ', whitespaceCount) + block.Value.CommentPrefix);
            }
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
                                adornmentOnLine.CurrentState = TeXCommentAdornmentState.EditingAndRenderingPreview;
                            }
                        }
                    }
                }
            }
        }

        private DateTime lastTimeZoomChanged;
#pragma warning disable VSTHRD100 // Avoid async void methods
        private async void ZoomChanged(IWpfTextView textView, double zoomPercentage)
        {
            //Zoom is changing continuously (while changing by mouse wheel).
            //We want to wait a moment before triggering invalidation (we hope that after moment changing is done).
            const int delayMs = 1000;
            var now = DateTime.Now;
            if ((now - lastTimeZoomChanged).TotalMilliseconds > delayMs)
            {
                lastTimeZoomChanged = now;
                await Task.Run(
                    async () =>
                    {
                        await Task.Delay(delayMs);
                        await Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        ForAllCurrentlyUsedAdornments(a => a.Invalidate(), false);
                    });
            }
        }
#pragma warning restore VSTHRD100 // Avoid async void methods

        private void CustomZoomChanged(double zoomScale)
        {
            ForAllCurrentlyUsedAdornments(a => a.Invalidate(), false);
        }

        private void ColorsChanged(IWpfTextView textView, SolidColorBrush foreground, SolidColorBrush background)
        {
            ForAllCurrentlyUsedAdornments(a => a.Invalidate(), false);
        }

        public override void Dispose()
        {
            if (isDisposed) return;

            try
            {
                base.Dispose();
                TextView.TextBuffer.Changed -= TextBuffer_Changed;
                vsSettings.ZoomChanged -= ZoomChanged;
                vsSettings.CommentsColorChanged -= ColorsChanged;
                ExtensionSettings.Instance.CustomZoomChanged -= CustomZoomChanged;
            }
            finally
            {
                isDisposed = true;
            }
        }

        protected override TeXCommentAdornment CreateAdornment(TeXCommentTag dataTag, ITextSnapshot snapshot)
        {
            var lineSpan = new LineSpan(
                snapshot.GetLineNumberFromPosition(dataTag.TeXBlock.Span.Start),
                snapshot.GetLineNumberFromPosition(dataTag.TeXBlock.Span.End));

            var lastLine = snapshot.GetLineFromLineNumber(lineSpan.LastLine);
            var lastLineWidthWithoutStartWhiteSpaces = (lastLine.Extent.Length - dataTag.TeXBlock.LastLineWhiteSpacesAtStart) * TextView.FormattedLineSource?.ColumnWidth;

            var adornment = new TeXCommentAdornment(
                TextView,
                dataTag,
                lineSpan,
                lastLineWidthWithoutStartWhiteSpaces ?? 0,
                textHasBeenEdited ? TeXCommentAdornmentState.EditingAndRenderingPreview : TeXCommentAdornmentState.Rendering,
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
                    ForAllCurrentlyUsedAdornments(a => a.CurrentState = isInEditMode ? TeXCommentAdornmentState.EditingAndRenderingPreview : TeXCommentAdornmentState.Rendering, false);
                },
                (tag, attributeText) =>
                {
                    var pos = tag.Span.Start + tag.TeXBlock.FirstLineWhiteSpacesAtStart + tag.TeXBlock.TeXCommentPrefix.Length + tag.TeXBlock.PropertiesSegmentLength;
                    Snapshot.TextBuffer.Insert(pos, $"[{attributeText}]");
                },
                renderingManager,
                vsSettings);
            TextView.TextBuffer.Changed += adornment.HandleTextBufferChanged;

            MarkAdornmentLines(lineSpan, adornment);

            return adornment;
        }

        protected override void UpdateAdornment(TeXCommentAdornment adornment, TeXCommentTag dataTag, ITextSnapshot snapshot)
        {
            var lineSpan = new LineSpan(
                snapshot.GetLineNumberFromPosition(dataTag.TeXBlock.Span.Start),
                snapshot.GetLineNumberFromPosition(dataTag.TeXBlock.Span.End));

            if (adornment.LineSpan != lineSpan)
            {
                MarkAdornmentLines(adornment.LineSpan, null); //remove old
                MarkAdornmentLines(lineSpan, adornment); //add new
            }

            var lastLine = snapshot.GetLineFromLineNumber(lineSpan.LastLine);
            var lastLineWidthWithoutStartWhiteSpaces = (lastLine.Extent.Length - dataTag.TeXBlock.LastLineWhiteSpacesAtStart) * TextView.FormattedLineSource?.ColumnWidth;

            adornment.Update(dataTag, lineSpan, lastLineWidthWithoutStartWhiteSpaces);
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