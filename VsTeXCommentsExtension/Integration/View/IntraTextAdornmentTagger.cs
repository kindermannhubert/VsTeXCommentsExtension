using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using VsTeXCommentsExtension.View;

namespace VsTeXCommentsExtension.Integration.View
{
    /// <summary>
    /// Helper class for interspersing adornments into text.
    /// </summary>
    /// <remarks>
    /// To avoid an issue around intra-text adornment support and its interaction with text buffer changes,
    /// this tagger reacts to text and tag changes with a delay. It waits to send out its own TagsChanged
    /// event until the WPF Dispatcher is running again and it takes care to report adornments
    /// that are consistent with the latest sent TagsChanged event by storing that particular snapshot
    /// and using it to query for the data tags.
    /// </remarks>
    internal abstract class IntraTextAdornmentTagger<TDataTag, TAdornment> : ITagger<IntraTextAdornmentTag>, IDisposable
        where TDataTag : ITag, ITagSpan
        where TAdornment : UIElement, ITagAdornment
    {
        private const int MaxAdornmentPoolSize = 128;

        private readonly List<SnapshotSpan> invalidatedSpans = new List<SnapshotSpan>();
        private readonly List<TAdornment> adornmentsPool = new List<TAdornment>(MaxAdornmentPoolSize);
        private readonly List<SnapshotSpan> editedBlockSpans = new List<SnapshotSpan>();
        private readonly TextSnapshotValuesPerVersionCache<PooledStructEnumerable<ITagSpan<IntraTextAdornmentTag>>> perSnapshotResults;
        private readonly ObjectPool<HashSet<AdornmentCacheKey>> adornmentCacheKeyHashsetsPool = new ObjectPool<HashSet<AdornmentCacheKey>>(() => new HashSet<AdornmentCacheKey>());
        private readonly ObjectPool<List<ITagSpan<IntraTextAdornmentTag>>> tagSpanListsPool = new ObjectPool<List<ITagSpan<IntraTextAdornmentTag>>>(() => new List<ITagSpan<IntraTextAdornmentTag>>());
        private readonly ObjectPool<Dictionary<AdornmentCacheKey, TAdornment>> adormentDictionariesPool = new ObjectPool<Dictionary<AdornmentCacheKey, TAdornment>>(() => new Dictionary<AdornmentCacheKey, TAdornment>());

        private Dictionary<AdornmentCacheKey, TAdornment> adornmentsCache = new Dictionary<AdornmentCacheKey, TAdornment>();
        private bool isEnabled;

        protected readonly TextSnapshotTeXCommentBlocks TexCommentBlocks;
        protected readonly IWpfTextView TextView;
        protected ITextSnapshot Snapshot { get; private set; }

        public IntraTextAdornmentTaggerDisplayMode Mode { get; set; }

        protected IntraTextAdornmentTagger(IWpfTextView textView)
        {
            this.TextView = textView;
            Snapshot = textView.TextBuffer.CurrentSnapshot;
            TexCommentBlocks = TextSnapshotTeXCommentBlocksProvider.Get(textView.TextBuffer);
            //this.view.LayoutChanged += HandleLayoutChanged;
            this.TextView.TextBuffer.Changed += HandleBufferChanged;
            ((FrameworkElement)textView).Loaded += TextView_Loaded;
            perSnapshotResults = new TextSnapshotValuesPerVersionCache<PooledStructEnumerable<ITagSpan<IntraTextAdornmentTag>>>(GetAdornmentTagsOnSnapshot);
        }

        /// <param name="span">The span of text that this adornment will elide.</param>
        /// <returns>Adornment corresponding to given data. May be null.</returns>
        protected abstract TAdornment CreateAdornment(TDataTag data, ITextSnapshot snapshot);

        protected abstract void UpdateAdornment(TAdornment adornment, TDataTag data, ITextSnapshot snapshot);

        /// <param name="spans">Spans to provide adornment data for. These spans do not necessarily correspond to text lines.</param>
        /// <remarks>
        /// If adornments need to be updated, call <see cref="RaiseTagsChanged"/> or <see cref="InvalidateSpans"/>.
        /// This will, indirectly, cause <see cref="GetAdornmentData"/> to be called.
        /// </remarks>
        /// <returns>
        /// A sequence of:
        ///  * adornment data for each adornment to be displayed
        ///  * the span of text that should be elided for that adornment (zero length spans are acceptable)
        ///  * and affinity of the adornment (this should be null if and only if the elided span has a length greater than zero)
        /// </returns>
        protected abstract IEnumerable<TagData> GetAdornmentData(NormalizedSnapshotSpanCollection spans);

        private void HandleBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            if (args.Changes.Count == 0) return;
            Debug.Assert(sender == TextView.TextBuffer);
            Debug.Assert(editedBlockSpans.Count == 0);

            foreach (var change in args.Changes)
            {
                using (var blockSpansBefore = TexCommentBlocks.GetBlockSpansIntersectedBy(args.Before, change.OldSpan))
                using (var blockSpansAfter = TexCommentBlocks.GetBlockSpansIntersectedBy(args.After, change.NewSpan))
                {
                    bool changed = false;
                    if (blockSpansBefore.Count == blockSpansAfter.Count)
                    {
                        for (int i = 0; i < blockSpansBefore.Count; i++)
                        {
                            if (blockSpansBefore[i].Span != blockSpansAfter[i].Span || blockSpansBefore[i].GetText() != blockSpansAfter[i].GetText())
                            {
                                changed = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        changed = true;
                    }

                    if (changed)
                    {
                        if (change.OldText.Contains(Environment.NewLine)) editedBlockSpans.AddRange(blockSpansBefore); //special case which causes an error in VS if not handled
                        editedBlockSpans.AddRange(blockSpansAfter);
                    }
                }
            }

            if (editedBlockSpans.Count == 0)
            {
                var changesStart = args.Changes[0].NewPosition;
                var changesEnd = args.Changes[args.Changes.Count - 1].NewEnd;
                editedBlockSpans.Add(new SnapshotSpan(args.After, new Span(changesStart, changesEnd - changesStart)));
            }

            InvalidateSpans(editedBlockSpans);
            editedBlockSpans.Clear();
        }

        protected void ForAllCurrentlyUsedAdornments(Action<TAdornment> action, bool invalidateAdornmentsAfterAction)
        {
            lock (adornmentsCache)
            {
                foreach (var adornment in adornmentsCache.Values)
                {
                    action(adornment);
                }

                if (invalidateAdornmentsAfterAction)
                {
                    InvalidateSpans(new List<SnapshotSpan>() { new SnapshotSpan(Snapshot, new Span(0, Snapshot.Length)) });
                }
            }
        }

        /// <summary>
        /// Causes intra-text adornments to be updated asynchronously.
        /// </summary>
        protected void InvalidateSpans(IList<SnapshotSpan> spans)
        {
            if (spans.Count == 0) return;

            bool wasEmpty = false;
            lock (invalidatedSpans)
            {
                wasEmpty = invalidatedSpans.Count == 0;
                invalidatedSpans.AddRangeByFor(spans);
            }

            if (wasEmpty)
                TextView.VisualElement.Dispatcher.BeginInvoke(new Action(AsyncUpdate));
        }

        private void AsyncUpdate()
        {
            // Store the snapshot that we're now current with and send an event
            // for the text that has changed.
            if (Snapshot != TextView.TextBuffer.CurrentSnapshot)
            {
                Snapshot = TextView.TextBuffer.CurrentSnapshot;

                var translatedAdornmentCache = adormentDictionariesPool.Get();
                Debug.Assert(translatedAdornmentCache.Count == 0);
                lock (adornmentsCache)
                {
                    foreach (var keyValuePair in adornmentsCache)
                    {
                        var newKey = new AdornmentCacheKey(keyValuePair.Key.Span.TranslateTo(Snapshot, SpanTrackingMode.EdgeExclusive));
                        if (!translatedAdornmentCache.ContainsKey(newKey))
                            translatedAdornmentCache.Add(newKey, keyValuePair.Value);
                    }

                    adornmentsCache.Clear();
                    adormentDictionariesPool.Put(adornmentsCache);
                    adornmentsCache = translatedAdornmentCache;
                }
            }

            int minSpan = int.MaxValue, maxSpan = int.MinValue;
            lock (invalidatedSpans)
            {
                if (invalidatedSpans.Count == 0) return;

                for (int i = 0; i < invalidatedSpans.Count; i++)
                {
                    var translatedSpan = invalidatedSpans[i].TranslateTo(Snapshot, SpanTrackingMode.EdgeInclusive);
                    if (translatedSpan.Start < minSpan) minSpan = translatedSpan.Start;
                    if (translatedSpan.End > maxSpan) maxSpan = translatedSpan.End;
                }

                invalidatedSpans.Clear();
            }

            RaiseTagsChanged(new SnapshotSpan(Snapshot, minSpan, maxSpan - minSpan));
        }

        /// <summary>
        /// Causes intra-text adornments to be updated synchronously.
        /// </summary>
        protected void RaiseTagsChanged(SnapshotSpan span)
        {
            Debug.WriteLine($"RaiseTagsChanged: {span}");
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }

        //private void HandleLayoutChanged(object sender, TextViewLayoutChangedEventArgs e)
        //{
        //    var visibleSpan = view.TextViewLines.FormattedSpan;

        //    // Filter out the adornments that are no longer visible.
        //    var toRemove =
        //        (from kv
        //         in adornmentCache
        //         where !kv.Key.Span.TranslateTo(visibleSpan.Snapshot, SpanTrackingMode.EdgeExclusive).IntersectsWith(visibleSpan)
        //         select kv.Key).ToList();

        //    foreach (var span in toRemove)
        //        adornmentCache.Remove(span);
        //}

        // Produces tags on the snapshot that the tag consumer asked for.
        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (!isEnabled || spans == null || spans.Count == 0) yield break;

            var requestedSnapshot = spans[0].Snapshot;
            var allTagSpans = perSnapshotResults.GetValue(Snapshot);
            foreach (var tagSpan in allTagSpans)
            {
                foreach (var span in spans)
                {
                    var adornment = (TeXCommentAdornment)tagSpan.Tag.Adornment;
                    var dataTagSpan = adornment.DataTag.SpanWithLastLineBreak;

                    var translatedDataTagSpan = new SnapshotSpan(Snapshot, dataTagSpan).TranslateTo(requestedSnapshot, SpanTrackingMode.EdgeExclusive);
                    if (translatedDataTagSpan.IntersectsWith(span))
                    {
                        var translatedTagSpan = new SnapshotSpan(Snapshot, tagSpan.Span).TranslateTo(requestedSnapshot, SpanTrackingMode.EdgeExclusive);
                        yield return new TagSpan<IntraTextAdornmentTag>(translatedTagSpan, tagSpan.Tag);
                        break;
                    }
                }
            }
        }

        private PooledStructEnumerable<ITagSpan<IntraTextAdornmentTag>> GetAdornmentTagsOnSnapshot(ITextSnapshot snapshot)
        {
            // Since WPF UI objects have state (like mouse hover or animation) and are relatively expensive to create and lay out,
            // this code tries to reuse controls as much as possible.
            // The controls are stored in this.adornmentCache between the calls.

            var results = tagSpanListsPool.Get();
            Debug.Assert(results.Count == 0);

            var toRemove = adornmentCacheKeyHashsetsPool.Get();
            Debug.Assert(toRemove.Count == 0);

            lock (adornmentsCache)
            {
                // Mark which adornments fall inside the requested spans with Keep=false
                // so that they can be removed from the cache if they no longer correspond to data tags.
                foreach (var kv in adornmentsCache)
                {
                    toRemove.Add(kv.Key);
                }

                foreach (var tagData in GetAdornmentData(new NormalizedSnapshotSpanCollection(snapshot, new Span(0, snapshot.Length))))
                {
                    // Look up the corresponding adornment or create one if it's new.
                    TAdornment adornment;
                    AdornmentInfo adornmentInfo;
                    var key = new AdornmentCacheKey(tagData.Span);
                    if (adornmentsCache.TryGetValue(key, out adornment))
                    {
                        adornmentInfo = tagData.GetAdornmentInfo(adornment.DisplayMode);
                        UpdateAdornment(adornment, tagData.Tag, snapshot);
                        toRemove.Remove(key);

                        Debug.WriteLine($"Updating adornment {adornment.Index}");
                    }
                    else
                    {
                        adornmentInfo = tagData.GetAdornmentInfo(Mode);

                        if (adornmentsPool.Count > 0)
                        {
                            adornment = adornmentsPool[adornmentsPool.Count - 1];
                            adornmentsPool.RemoveAt(adornmentsPool.Count - 1);
                            UpdateAdornment(adornment, tagData.Tag, snapshot);
                            adornment.CurrentState = TeXCommentAdornmentState.EditingAndRenderingPreview;
                            Debug.WriteLine($"Reusing adornment {adornment.Index} from pool");
                        }
                        else
                        {
                            adornment = CreateAdornment(tagData.Tag, snapshot);
                            adornment.DisplayModeChanged += (s, e) => perSnapshotResults.UpdateValue(Snapshot); //will be deattached on  dispose
                            Debug.WriteLine($"Creating adornment {adornment.Index}");
                        }

                        if (adornment == null) continue;

                        // Get the adornment to measure itself. Its DesiredSize property is used to determine
                        // how much space to leave between text for this adornment.
                        // Note: If the size of the adornment changes, the line will be reformatted to accommodate it.
                        // Note: Some adornments may change size when added to the view's visual tree due to inherited
                        // dependency properties that affect layout. Such options can include SnapsToDevicePixels,
                        // UseLayoutRounding, TextRenderingMode, TextHintingMode, and TextFormattingMode. Making sure
                        // that these properties on the adornment match the view's values before calling Measure here
                        // can help avoid the size change and the resulting unnecessary re-format.
                        adornment.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                        adornmentsCache.Add(key, adornment);
                    }

                    Debug.WriteLine($"Yielding adornment {adornment.Index} with span {adornmentInfo.Span}");
                    results.Add(new TagSpan<IntraTextAdornmentTag>(adornmentInfo.Span, new IntraTextAdornmentTag(adornment, null, adornmentInfo.Affinity)));
                }

                foreach (var adornmentKey in toRemove)
                {
                    if (adornmentsPool.Count < MaxAdornmentPoolSize)
                    {
                        adornmentsPool.Add(adornmentsCache[adornmentKey]);
                    }
                    adornmentsCache.Remove(adornmentKey);
                }

                toRemove.Clear();
                adornmentCacheKeyHashsetsPool.Put(toRemove);
            }

            return new PooledStructEnumerable<ITagSpan<IntraTextAdornmentTag>>(results, tagSpanListsPool);
        }


        private void TextView_Loaded(object sender, RoutedEventArgs e)
        {
            isEnabled = true;
        }

        public virtual void Dispose()
        {
            ((FrameworkElement)TextView).Loaded -= TextView_Loaded;

            TextSnapshotTeXCommentBlocksProvider.Release(TextView.TextBuffer, TexCommentBlocks);
            perSnapshotResults.Dispose();

            foreach (var item in adornmentsCache.Values) item.Dispose();
            adornmentsCache.Clear();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        private struct AdornmentCacheKey : IEquatable<AdornmentCacheKey>
        {
            private readonly int SnapshotVersion;
            private readonly int SpanStart;

            public readonly SnapshotSpan Span;

            public AdornmentCacheKey(SnapshotSpan span)
            {
                Span = span;
                SnapshotVersion = span.Snapshot.Version.VersionNumber;
                SpanStart = span.Start.Position;
            }

            public bool Equals(AdornmentCacheKey other)
            {
                return SnapshotVersion == other.SnapshotVersion && SpanStart == other.SpanStart;
            }

            public override int GetHashCode() => SnapshotVersion ^ SpanStart;
        }

        public struct TagData : IEquatable<TagData>
        {
            private readonly PositionAffinity? Affinity;
            public readonly SnapshotSpan Span;
            public readonly TDataTag Tag;

            public TagData(SnapshotSpan tagSpan, PositionAffinity? adornmentAffinity, TDataTag tag)
            {
                this.Span = tagSpan;
                Affinity = adornmentAffinity;
                Tag = tag;
            }

            public AdornmentInfo GetAdornmentInfo(IntraTextAdornmentTaggerDisplayMode mode)
            {
                switch (mode)
                {
                    case IntraTextAdornmentTaggerDisplayMode.HideOriginalText_WithoutLastLineBreak:
                        {
                            var affinity = Span.Length > 0 ? null : Affinity;
                            return new AdornmentInfo(Span, affinity);
                        }
                    case IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText_BeforeLastLineBreak:
                        {
                            SnapshotPoint start;
                            if (!Affinity.HasValue || Affinity.Value == PositionAffinity.Predecessor)
                            {
                                start = Span.Start;
                            }
                            else
                            {
                                start = Span.End;
                            }
                            var adornmentSpan = new SnapshotSpan(start, 0);
                            return new AdornmentInfo(adornmentSpan, Affinity);
                        }
                    default:
                        throw new InvalidOperationException($"Unknown {nameof(IntraTextAdornmentTaggerDisplayMode)}: '{mode}'.");
                }
            }

            public bool Equals(TagData other)
            {
                return Span.Snapshot.Version.VersionNumber == other.Span.Snapshot.Version.VersionNumber &&
                    Span.Start.Position == other.Span.Start.Position;
            }
        }

        public struct AdornmentInfo
        {
            public readonly SnapshotSpan Span;
            public readonly PositionAffinity? Affinity;

            public AdornmentInfo(SnapshotSpan adornmentSpan, PositionAffinity? affinity)
            {
                Span = adornmentSpan;
                Affinity = affinity;
            }
        }
    }

    public enum IntraTextAdornmentTaggerDisplayMode
    {
        HideOriginalText_WithoutLastLineBreak,
        DoNotHideOriginalText_BeforeLastLineBreak,
    }
}
