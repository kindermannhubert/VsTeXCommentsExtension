using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Drawing;
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
        private readonly SolidColorBrush commentsForegroundColor;
        private readonly Font textEditorFont;
        private readonly List<TeXCommentAdornment> linesWithAdornments = new List<TeXCommentAdornment>();

        internal static ITagger<IntraTextAdornmentTag> GetTagger(
            IWpfTextView view,
            Lazy<ITagAggregator<TeXCommentTag>> texCommentTagger,
            IEditorFormatMapService editorFormatMapService,
            IRenderingManager renderingManager,
            Font textEditorFont)
        {
            var commentsForegroundColor = System.Windows.Media.Brushes.Black;
            try
            {
                commentsForegroundColor = (SolidColorBrush)editorFormatMapService.GetEditorFormatMap("Text Editor").GetProperties("Comment")["Foreground"];
            }
            catch { }

            return view.Properties.GetOrCreateSingletonProperty(
                () => new TeXCommentAdornmentTagger(view, renderingManager, texCommentTagger.Value, commentsForegroundColor, textEditorFont));
        }

        private TeXCommentAdornmentTagger(
            IWpfTextView view,
            IRenderingManager renderingManager,
            ITagAggregator<TeXCommentTag> texCommentTagger,
            SolidColorBrush commentsForegroundColor,
            Font textEditorFont)
            : base(view, texCommentTagger, IntraTextAdornmentTaggerDisplayMode.HideOriginalText)
        {
            this.renderingManager = renderingManager;
            this.commentsForegroundColor = commentsForegroundColor;
            this.textEditorFont = textEditorFont;
            view.TextBuffer.Changed += TextBuffer_Changed;
        }

        private void TextBuffer_Changed(object sender, TextContentChangedEventArgs e)
        {
            //ignoring new lines
            if (e.Changes.Count == 1)
            {
                var change = e.Changes[0];
                if (change.OldText == Environment.NewLine || change.NewText == Environment.NewLine) return;
            }

            //when we start editing line with adornment we switch to edit mode
            var line = Snapshot.GetLineNumberFromPosition(view.Caret.Position.BufferPosition.Position);
            var adornmentOnLine = GetAdornmentOnLine(line);
            if (adornmentOnLine != null && !adornmentOnLine.IsInEditMode)
            {
                adornmentOnLine.IsInEditMode = true;
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
                commentsForegroundColor.Color,
                (view.Background as SolidColorBrush)?.Color ?? Colors.White,
                textEditorFont,
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
