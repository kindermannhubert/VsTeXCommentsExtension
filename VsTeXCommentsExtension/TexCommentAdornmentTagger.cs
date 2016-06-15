using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Windows.Media;

namespace VsTeXCommentsExtension
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
        internal static ITagger<IntraTextAdornmentTag> GetTagger(IWpfTextView view, Lazy<ITagAggregator<TeXCommentTag>> texCommentTagger)
        {
            return view.Properties.GetOrCreateSingletonProperty(
                () => new TeXCommentAdornmentTagger(view, texCommentTagger.Value));
        }

        private TeXCommentAdornmentTagger(IWpfTextView view, ITagAggregator<TeXCommentTag> TexCommentTagger)
            : base(view, TexCommentTagger, IntraTextAdornmentTaggerDisplayMode.HideOriginalText)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            view.Properties.RemoveProperty(typeof(TeXCommentAdornmentTagger));
        }

        protected override TeXCommentAdornment CreateAdornment(TeXCommentTag dataTag, Span adornmentSpan, IntraTextAdornmentTaggerDisplayMode defaultDisplayMode)
        {
            var adornment = new TeXCommentAdornment(
                dataTag,
                (view.Background as SolidColorBrush)?.Color ?? Colors.White,
                span =>
                {
                    //var blockSpans = texCommentBlocks.GetBlockSpansWithLastLineBreakIntersectedBy(Snapshot, span);
                    //foreach (var blockSpan in blockSpans)
                    //{
                    //    RaiseTagsChanged(new SnapshotSpan(Snapshot, blockSpan));
                    //}
                    RaiseTagsChanged(new SnapshotSpan(Snapshot, 0, Snapshot.Length));
                },
                defaultDisplayMode);
            view.TextBuffer.Changed += adornment.HandleTextBufferChanged;

            return adornment;
        }

        protected override bool UpdateAdornment(TeXCommentAdornment adornment, TeXCommentTag dataTag)
        {
            adornment.Update(dataTag);
            return true;
        }
    }
}
