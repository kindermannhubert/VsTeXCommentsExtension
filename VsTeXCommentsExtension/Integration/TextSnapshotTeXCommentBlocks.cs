using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Linq;

namespace VsTeXCommentsExtension.Integration
{
    class TextSnapshotTeXCommentBlocks
    {
        private const int VersionsToCache = 8;
        private const int CachedVersionToRemoveOnCleanUp = 4;

        public const string CommentPrefix = "//";
        public const string TeXCommentPrefix = "//tex:";
        public static readonly char[] WhiteSpaces = new char[] { ' ', '\t' };

        private readonly Dictionary<int, List<TeXCommentBlockSpan>> blocksPerVersion = new Dictionary<int, List<TeXCommentBlockSpan>>();
        private readonly List<int> versions = new List<int>();

        public IReadOnlyList<TeXCommentBlockSpan> GetTexCommentBlocks(ITextSnapshot snapshot)
        {
            var version = snapshot.Version.VersionNumber;

            List<TeXCommentBlockSpan> blocks;
            if (!blocksPerVersion.TryGetValue(version, out blocks))
            {
                blocks = GenerateTexCommentBlocks(snapshot);
                blocksPerVersion.Add(version, blocks);
                versions.Add(-version);
            }

            if (versions.Count > VersionsToCache)
            {
                versions.Sort();
                for (int i = versions.Count - 1; i >= VersionsToCache - CachedVersionToRemoveOnCleanUp; i--)
                {
                    blocksPerVersion.Remove(-versions[i]);
                }
                versions.RemoveRange(VersionsToCache - CachedVersionToRemoveOnCleanUp, CachedVersionToRemoveOnCleanUp);
            }

            return blocks;
        }

        //TODO perf/allocations/List<>pooling
        private static List<TeXCommentBlockSpan> GenerateTexCommentBlocks(ITextSnapshot snapshot)
        {
            var texCommentBlocks = new List<TeXCommentBlockSpan>();
            var atTexBlock = false;
            var texBlockSpan = default(TeXCommentBlockSpan);
            int lastBlockLineBreakLength = 0;
            foreach (var line in snapshot.Lines)
            {
                var lineText = line.GetText();
                var lineTextTrimmed = lineText.TrimStart(WhiteSpaces);
                if (atTexBlock)
                {
                    if (lineTextTrimmed.StartsWith(TeXCommentPrefix))
                    {
                        texBlockSpan.RemoveLastLineBreak(lastBlockLineBreakLength);
                        texCommentBlocks.Add(texBlockSpan); //end of current block

                        texBlockSpan = new TeXCommentBlockSpan(line.ExtentIncludingLineBreak, lineText.Length - lineTextTrimmed.Length, line.GetLineBreakText()); //start of new block
                        lastBlockLineBreakLength = line.LineBreakLength;
                    }
                    else if (lineTextTrimmed.StartsWith(CommentPrefix))
                    {
                        //continuation of current block
                        texBlockSpan.Add(line.LengthIncludingLineBreak);
                        lastBlockLineBreakLength = line.LineBreakLength;
                    }
                    else
                    {
                        //end of current block
                        texBlockSpan.RemoveLastLineBreak(lastBlockLineBreakLength);
                        texCommentBlocks.Add(texBlockSpan);
                        atTexBlock = false;
                    }
                }
                else if (lineTextTrimmed.StartsWith(TeXCommentPrefix))
                {
                    //start of new block
                    atTexBlock = true;
                    texBlockSpan = new TeXCommentBlockSpan(line.ExtentIncludingLineBreak, lineText.Length - lineTextTrimmed.Length, line.GetLineBreakText());
                    lastBlockLineBreakLength = line.LineBreakLength;
                }
            }
            if (atTexBlock)
            {
                texBlockSpan.RemoveLastLineBreak(lastBlockLineBreakLength);
                texCommentBlocks.Add(texBlockSpan);
            }

            return texCommentBlocks;
        }

        //TODO perf/allocations/List<>pooling
        public IReadOnlyList<SnapshotSpan> GetBlockSpansIntersectedBy(ITextSnapshot snapshot, Span span)
        {
            var blocks = GetTexCommentBlocks(snapshot);

            var results = new List<SnapshotSpan>();
            foreach (var block in blocks.Where(b => b.Span.IntersectsWith(span)))
            {
                results.Add(new SnapshotSpan(snapshot, block.Span));
            }

            return results;
        }

        //TODO perf/allocations/List<>pooling
        public IReadOnlyList<SnapshotSpan> GetBlockSpansWithLastLineBreakIntersectedBy(ITextSnapshot snapshot, Span span)
        {
            var blocks = GetTexCommentBlocks(snapshot);

            var results = new List<SnapshotSpan>();
            foreach (var block in blocks.Where(b => b.SpanWithLastLineBreak.IntersectsWith(span)))
            {
                results.Add(new SnapshotSpan(snapshot, block.SpanWithLastLineBreak));
            }

            return results;
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
