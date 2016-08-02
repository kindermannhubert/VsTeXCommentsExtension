﻿using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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

            var snapshot = spans[0].Snapshot;
            var allTagSpans = tagsPerVersion.GetValue(snapshot);
            for (int spanIndex = 0; spanIndex < spans.Count; spanIndex++)
            {
                var span = spans[spanIndex];
                foreach (var tagSpan in allTagSpans)
                {
                    if (span.IntersectsWith(tagSpan.Span))
                    {
                        yield return tagSpan;
                    }
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
