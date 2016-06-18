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
        public const string TexCommentPrefix = "//tex:";
        public static readonly char[] WhiteSpaces = new char[] { ' ', '\t' };

        private readonly Dictionary<int, List<TeXCommentBlock>> blocksPerVersion = new Dictionary<int, List<TeXCommentBlock>>();
        private readonly List<int> versions = new List<int>();

        public IReadOnlyList<TeXCommentBlock> GetTexCommentBlocks(ITextSnapshot snapshot)
        {
            var version = snapshot.Version.VersionNumber;

            List<TeXCommentBlock> blocks;
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
        private static List<TeXCommentBlock> GenerateTexCommentBlocks(ITextSnapshot snapshot)
        {
            var texCommentBlocks = new List<TeXCommentBlock>();
            var atTexBlock = false;
            var texBlockSpan = default(TeXCommentBlock);
            int lastBlockLineBreakLength = 0;
            foreach (var line in snapshot.Lines)
            {
                var lineText = line.GetText();
                var lineTextTrimmed = lineText.TrimStart(WhiteSpaces);
                if (atTexBlock)
                {
                    if (lineTextTrimmed.StartsWith(TexCommentPrefix))
                    {
                        texBlockSpan.RemoveLastLineBreak(lastBlockLineBreakLength);
                        texCommentBlocks.Add(texBlockSpan); //end of current block

                        texBlockSpan = new TeXCommentBlock(line.ExtentIncludingLineBreak, lineText.Length - lineTextTrimmed.Length, line.GetLineBreakText()); //start of new block
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
                else if (lineTextTrimmed.StartsWith(TexCommentPrefix))
                {
                    //start of new block
                    atTexBlock = true;
                    texBlockSpan = new TeXCommentBlock(line.ExtentIncludingLineBreak, lineText.Length - lineTextTrimmed.Length, line.GetLineBreakText());
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
    }
}
