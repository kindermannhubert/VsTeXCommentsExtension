using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace VsTeXCommentsExtension.Integration.Data
{
    /// <summary>
    /// Determines which spans of text likely refer TeX comment blocks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a data-only component. The tagging system is a good fit for presenting data-about-text.
    /// The <see cref="TeXCommentAdornmentTagger"/> takes tags produced by this tagger and creates corresponding UI for this data.
    /// </para>
    /// </remarks>
    internal sealed class TeXCommentTagger : ITagger<TeXCommentTag>, IDisposable
    {
        private readonly ObjectPool<List<bool>> boolListsPool = new ObjectPool<List<bool>>(() => new List<bool>());
        private readonly ObjectPool<List<ITagSpan<TeXCommentTag>>> tagSpanListsPool = new ObjectPool<List<ITagSpan<TeXCommentTag>>>(() => new List<ITagSpan<TeXCommentTag>>());
        private readonly TextSnapshotValuesPerVersionCache<PooledStructEnumerable<ITagSpan<TeXCommentTag>>> tagsPerVersion;
        private readonly ITextBuffer buffer;
        private readonly TextSnapshotTeXCommentBlocks texCommentBlocks;

        private bool isDisposed;

        internal TeXCommentTagger(ITextBuffer buffer)
        {
            this.buffer = buffer;
            tagsPerVersion = new TextSnapshotValuesPerVersionCache<PooledStructEnumerable<ITagSpan<TeXCommentTag>>>(GenerateAllTags);
            texCommentBlocks = TextSnapshotTeXCommentBlocksProvider.Get(buffer);

            //buffer.Changed += (sender, args) => HandleBufferChanged(args);
        }

#pragma warning disable CS0067 // The event is never used
        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
#pragma warning restore CS0067 // The event is never used

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

        public IEnumerable<ITagSpan<TeXCommentTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans.Count == 0) yield break;

            var firstSpan = spans[0];
            var allTagSpans = tagsPerVersion.GetValue(firstSpan.Snapshot);
            if (spans.Count == 1)
            {
                if (firstSpan.Start == 0 && firstSpan.Length == firstSpan.Snapshot.Length)
                {
                    //span is over whole snapshot, so we can return all tagspans without filtering
                    foreach (var tagSpan in allTagSpans) yield return tagSpan;
                }
                else
                {
                    bool foundAnyInCurrentSpan = false;
                    int tagSpanIndex = 0;
                    foreach (var tagSpan in allTagSpans)
                    {
                        if (firstSpan.IntersectsWith(tagSpan.Span))
                        {
                            foundAnyInCurrentSpan = true;
                            yield return tagSpan;
                        }
                        else if (foundAnyInCurrentSpan)
                        {
                            //tagspans are ordered, so we can stop searching here
                            break;
                        }

                        ++tagSpanIndex;
                    }
                }
            }
            else
            {
                //we don't want to report any tagSpans multiple times, so we keep track of already yielded ones
                List<bool> tagSpanYielded;
                lock (boolListsPool)
                {
                    tagSpanYielded = boolListsPool.Get();
                }
                for (int i = 0; i < allTagSpans.Count; i++) tagSpanYielded.Add(false);

                for (int spanIndex = 0; spanIndex < spans.Count; spanIndex++)
                {
                    var span = spans[spanIndex];

                    bool foundAnyInCurrentSpan = false;
                    int tagSpanIndex = 0;
                    foreach (var tagSpan in allTagSpans)
                    {
                        if (!tagSpanYielded[tagSpanIndex])
                        {
                            if (span.IntersectsWith(tagSpan.Span))
                            {
                                foundAnyInCurrentSpan = true;
                                tagSpanYielded[tagSpanIndex] = true;
                                yield return tagSpan;
                            }
                            else if (foundAnyInCurrentSpan)
                            {
                                //tagspans are ordered, so we can stop searching here
                                break;
                            }
                        }

                        ++tagSpanIndex;
                    }
                }

                tagSpanYielded.Clear();
                lock (boolListsPool)
                {
                    boolListsPool.Put(tagSpanYielded);
                }
            }
        }

        private PooledStructEnumerable<ITagSpan<TeXCommentTag>> GenerateAllTags(ITextSnapshot snapshot)
        {
            var results = tagSpanListsPool.Get();
            Debug.Assert(results.Count == 0);

            foreach (var block in texCommentBlocks.GetTexCommentBlocks(snapshot))
            {
                var firstLine = snapshot.GetLineFromPosition(block.Span.Start);
                var lastLine = snapshot.GetLineFromPosition(block.Span.End);

                var tag = block.GetDataTag(snapshot);
                var translatedBlockSpan = new Span(block.Span.Start + block.FirstLineWhiteSpacesAtStart, block.Span.Length - block.FirstLineWhiteSpacesAtStart);
                results.Add(new TagSpan<TeXCommentTag>(new SnapshotSpan(snapshot, translatedBlockSpan), tag));
            }

            return new PooledStructEnumerable<ITagSpan<TeXCommentTag>>(results, tagSpanListsPool);
        }

        ///// <summary>
        ///// Handle buffer changes. The default implementation expands changes to full lines and sends out
        ///// a <see cref="TagsChanged"/> event for these lines.
        ///// </summary>
        ///// <param name="args">The buffer change arguments.</param>
        //private void HandleBufferChanged(TextContentChangedEventArgs args)
        //{
        //    if (args.Changes.Count == 0) return;

        //    var tagsChanged = TagsChanged;
        //    if (tagsChanged == null) return;

        //    // Combine all changes into a single span so that
        //    // the ITagger<>.TagsChanged event can be raised just once for a compound edit
        //    // with many parts.

        //    var snapshot = args.After;

        //    var start = args.Changes[0].NewPosition;
        //    var end = args.Changes[args.Changes.Count - 1].NewEnd;

        //    var totalAffectedSpan = new SnapshotSpan(
        //        snapshot.GetLineFromPosition(start).Start,
        //        snapshot.GetLineFromPosition(end).End);

        //    tagsChanged(this, new SnapshotSpanEventArgs(totalAffectedSpan));
        //}
    }
}