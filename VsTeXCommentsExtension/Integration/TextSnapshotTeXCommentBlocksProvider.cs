using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;

namespace VsTeXCommentsExtension.Integration
{
    internal static class TextSnapshotTeXCommentBlocksProvider
    {
        private static readonly Dictionary<ITextBuffer, CachedItem> Cache = new Dictionary<ITextBuffer, CachedItem>();

        public static TextSnapshotTeXCommentBlocks Get(ITextBuffer textBuffer)
        {
            lock (Cache)
            {
                CachedItem item;
                if (!Cache.TryGetValue(textBuffer, out item))
                {
                    TextSnapshotTeXCommentBlocks tSTCB = new TextSnapshotTeXCommentBlocks();
                    item = new CachedItem(new TextSnapshotTeXCommentBlocks());
                    Cache.Add(textBuffer, item);
                    return item.Blocks;
                }

                ++item.Counter;
                return item.Blocks;
            }
        }

        public static void Release(ITextBuffer textBuffer, TextSnapshotTeXCommentBlocks blocks)
        {
            lock (Cache)
            {
                CachedItem item;
                if (!Cache.TryGetValue(textBuffer, out item) || item.Blocks != blocks) throw new InvalidOperationException("Releasing of invalid blocks.");

                if (--item.Counter == 0)
                {
                    Cache.Remove(textBuffer);
                }
            }
        }

        private class CachedItem
        {
            public readonly TextSnapshotTeXCommentBlocks Blocks;
            public int Counter;

            public CachedItem(TextSnapshotTeXCommentBlocks blocks)
            {
                Blocks = blocks;
                Counter = 1;
            }
        }
    }
}
