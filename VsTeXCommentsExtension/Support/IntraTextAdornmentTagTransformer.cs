using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Windows;

namespace VsTeXCommentsExtension
{
    /// <summary>
    /// Helper class for producing intra-text adornments from data tags.
    /// </summary>
    /// <remarks>
    /// For cases where intra-text adornments do not correspond exactly to tags,
    /// use the <see cref="IntraTextAdornmentTagger"/> base class.
    /// </remarks>
    internal abstract class IntraTextAdornmentTagTransformer<TDataTag, TAdornment>
        : IntraTextAdornmentTagger<TDataTag, TAdornment>, IDisposable
        where TDataTag : ITag
        where TAdornment : UIElement, ITagAdornment
    {
        protected readonly ITagAggregator<TDataTag> dataTagger;
        protected readonly PositionAffinity? adornmentAffinity;

        /// <param name="adornmentAffinity">Determines whether adornments based on data tags with zero-length spans
        /// will stick with preceding or succeeding text characters.</param>
        protected IntraTextAdornmentTagTransformer(
            IWpfTextView view, ITagAggregator<TDataTag> dataTagger,
            IntraTextAdornmentTaggerDisplayMode mode,
            PositionAffinity adornmentAffinity = PositionAffinity.Successor)
            : base(view)
        {
            this.adornmentAffinity = adornmentAffinity;
            this.dataTagger = dataTagger;
            this.dataTagger.TagsChanged += HandleDataTagsChanged;
            Mode = mode;
        }

        protected override IEnumerable<TagData> GetAdornmentData(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0) yield break;

            var snapshot = spans[0].Snapshot;
            foreach (var dataTagSpan in dataTagger.GetTags(spans))
            {
                var dataTagSpans = dataTagSpan.Span.GetSpans(snapshot);

                // Ignore data tags that are split by projection.
                // This is theoretically possible but unlikely in current scenarios.
                if (dataTagSpans.Count != 1) continue;

                yield return new TagData(dataTagSpans[0], adornmentAffinity, dataTagSpan.Tag);
            }
        }

        private void HandleDataTagsChanged(object sender, TagsChangedEventArgs args)
        {
            var changedSpans = args.Span.GetSpans(view.TextBuffer.CurrentSnapshot);
            InvalidateSpans(changedSpans);
        }

        public virtual void Dispose()
        {
            dataTagger.Dispose();
        }
    }
}
