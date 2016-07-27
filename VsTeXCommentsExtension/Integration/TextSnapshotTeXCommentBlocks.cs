using Microsoft.VisualStudio.Text;
using System.Collections.Generic;

namespace VsTeXCommentsExtension.Integration
{
    internal class TextSnapshotTeXCommentBlocks
    {
        private const int VersionsToCache = 16;
        private const int CachedVersionsToRemoveOnCleanUp = 13; //clean up will run when we have VersionsToCache+1 versions

        public const string CommentPrefix = "//";
        public const string TeXCommentPrefix = "//tex:";
        public static readonly char[] WhiteSpaces = new char[] { ' ', '\t' };

        private readonly Dictionary<int, List<TeXCommentBlockSpan>> blocksPerVersion = new Dictionary<int, List<TeXCommentBlockSpan>>();
        private readonly List<int> versions = new List<int>();

        private readonly ObjectPool<List<TeXCommentBlockSpan>> blockListsPool = new ObjectPool<List<TeXCommentBlockSpan>>(() => new List<TeXCommentBlockSpan>());
        private readonly ObjectPool<List<SnapshotSpan>> snapshotSpansListsPool = new ObjectPool<List<SnapshotSpan>>(() => new List<SnapshotSpan>());

        public IReadOnlyList<TeXCommentBlockSpan> GetTexCommentBlocks(ITextSnapshot snapshot)
        {
            lock (versions)
            {
                var version = snapshot.Version.VersionNumber;

                List<TeXCommentBlockSpan> blocks;
                if (!blocksPerVersion.TryGetValue(version, out blocks))
                {
                    blocks = GenerateTexCommentBlocks(snapshot);
                    blocksPerVersion.Add(version, blocks);
                    versions.Add(-version);

                    DismissOldVersions();
                }

                return blocks;
            }
        }

        private void DismissOldVersions()
        {
            if (versions.Count > VersionsToCache)
            {
                versions.Sort();
                var lastIndex = versions.Count - CachedVersionsToRemoveOnCleanUp;
                for (int i = versions.Count - 1; i >= lastIndex; i--)
                {
                    var versionToRemove = -versions[i];
                    var removedBlocksList = blocksPerVersion[versionToRemove];
                    blocksPerVersion.Remove(versionToRemove);

                    removedBlocksList.Clear();
                    blockListsPool.Put(removedBlocksList);
                }
                versions.RemoveRange(versions.Count - CachedVersionsToRemoveOnCleanUp, CachedVersionsToRemoveOnCleanUp);
            }
        }

        private List<TeXCommentBlockSpan> GenerateTexCommentBlocks(ITextSnapshot snapshot)
        {
            var texCommentBlocks = blockListsPool.Get();
            var atTexBlock = false;
            var texBlockSpanBuilder = default(TeXCommentBlockSpanBuilder);
            ITextSnapshotLine lastBlockLine = null;
            foreach (var line in snapshot.Lines)
            {
                var lineText = line.GetText();
                var numberOfWhiteSpaceCharsOnStartOfLine = lineText.NumberOfWhiteSpaceCharsOnStartOfLine();

                if (atTexBlock)
                {
                    if (lineText.StartsWith(numberOfWhiteSpaceCharsOnStartOfLine, TeXCommentPrefix))
                    {
                        texBlockSpanBuilder.EndBlock(lastBlockLine);
                        texCommentBlocks.Add(texBlockSpanBuilder.Build(snapshot)); //end of current block

                        texBlockSpanBuilder = new TeXCommentBlockSpanBuilder(line.ExtentIncludingLineBreak, numberOfWhiteSpaceCharsOnStartOfLine, line.GetLineBreakText()); //start of new block
                        lastBlockLine = line;
                    }
                    else if (lineText.StartsWith(numberOfWhiteSpaceCharsOnStartOfLine, CommentPrefix))
                    {
                        //continuation of current block
                        texBlockSpanBuilder.Add(line.LengthIncludingLineBreak);
                        lastBlockLine = line;
                    }
                    else
                    {
                        //end of current block
                        texBlockSpanBuilder.EndBlock(lastBlockLine);
                        texCommentBlocks.Add(texBlockSpanBuilder.Build(snapshot));
                        atTexBlock = false;
                    }
                }
                else if (lineText.StartsWith(numberOfWhiteSpaceCharsOnStartOfLine, TeXCommentPrefix))
                {
                    //start of new block
                    atTexBlock = true;
                    texBlockSpanBuilder = new TeXCommentBlockSpanBuilder(line.ExtentIncludingLineBreak, numberOfWhiteSpaceCharsOnStartOfLine, line.GetLineBreakText());
                    lastBlockLine = line;
                }
            }
            if (atTexBlock)
            {
                texBlockSpanBuilder.EndBlock(lastBlockLine);
                texCommentBlocks.Add(texBlockSpanBuilder.Build(snapshot));
            }

            return texCommentBlocks;
        }

        public PooledStructEnumerable<SnapshotSpan> GetBlockSpansIntersectedBy(ITextSnapshot snapshot, Span span)
        {
            var blocks = GetTexCommentBlocks(snapshot);

            var results = snapshotSpansListsPool.Get();
            foreach (var block in blocks)
            {
                if (block.Span.IntersectsWith(span))
                {
                    results.Add(new SnapshotSpan(snapshot, block.Span));
                }
            }

            return new PooledStructEnumerable<SnapshotSpan>(results, snapshotSpansListsPool);
        }

        public PooledStructEnumerable<SnapshotSpan> GetBlockSpansWithLastLineBreakIntersectedBy(ITextSnapshot snapshot, Span span)
        {
            var blocks = GetTexCommentBlocks(snapshot);

            var results = snapshotSpansListsPool.Get();
            foreach (var block in blocks)
            {
                if (block.SpanWithLastLineBreak.IntersectsWith(span))
                {
                    results.Add(new SnapshotSpan(snapshot, block.SpanWithLastLineBreak));
                }
            }

            return new PooledStructEnumerable<SnapshotSpan>(results, snapshotSpansListsPool);
        }

        public TeXCommentBlockSpan? GetBlockForPosition(ITextSnapshot snapshot, int position)
        {
            var blocks = GetTexCommentBlocks(snapshot);

            foreach (var block in blocks)
            {
                if (block.Span.Contains(position)) return block;
            }

            return null;
        }
    }
}
