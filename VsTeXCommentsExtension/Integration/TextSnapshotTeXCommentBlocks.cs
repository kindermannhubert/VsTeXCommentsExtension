using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.Text;

namespace VsTeXCommentsExtension.Integration
{
    internal class TextSnapshotTeXCommentBlocks
    {
        public static readonly HashSet<string> SupportedContentTypes = new HashSet<string>() { "CSharp", "F#", "C/C++", "Basic", "Python", "R" };

        public static readonly Dictionary<string, string> CommentPrefixPerContentType = new Dictionary<string, string>()
        {
            { "CSharp", "//" },
            { "F#", "//" },
            { "C/C++", "//" },
            { "Basic", "'" },
            { "Python", "#" },
            { "R", "#" },
        };

        public static readonly Dictionary<string, string> TeXCommentPrefixPerContentType = new Dictionary<string, string>()
        {
            { "CSharp", "//tex:" },
            { "F#", "//tex:" },
            { "C/C++", "//tex:" },
            { "Basic", "'tex:" },
            { "Python", "#tex:" },
            { "R", "#tex:" },
        };

        public static readonly char[] WhiteSpaces = new char[] { ' ', '\t' };

        private readonly TextSnapshotValuesPerVersionCache<PooledStructEnumerable<TeXCommentBlockSpan>> blocksPerVersion;

        private readonly ObjectPool<List<TeXCommentBlockSpan>> blockListsPool = new ObjectPool<List<TeXCommentBlockSpan>>(() => new List<TeXCommentBlockSpan>());
        private readonly ObjectPool<List<SnapshotSpan>> snapshotSpansListsPool = new ObjectPool<List<SnapshotSpan>>(() => new List<SnapshotSpan>());

        public TextSnapshotTeXCommentBlocks()
        {
            blocksPerVersion = new TextSnapshotValuesPerVersionCache<PooledStructEnumerable<TeXCommentBlockSpan>>(GenerateTexCommentBlocks);

            Debug.Assert(SupportedContentTypes.All(type => CommentPrefixPerContentType.ContainsKey(type) && TeXCommentPrefixPerContentType.ContainsKey(type)));
        }

        public StructEnumerable<TeXCommentBlockSpan> GetTexCommentBlocks(ITextSnapshot snapshot) => blocksPerVersion.GetValue(snapshot);

        private PooledStructEnumerable<TeXCommentBlockSpan> GenerateTexCommentBlocks(ITextSnapshot snapshot)
        {
            var texCommentBlocks = blockListsPool.Get();

            var contentName = snapshot.ContentType.TypeName;
            if (!CommentPrefixPerContentType.TryGetValue(contentName, out var commentPrefix) ||
                !TeXCommentPrefixPerContentType.TryGetValue(contentName, out var teXCommentPrefix))
            {
                return new PooledStructEnumerable<TeXCommentBlockSpan>(texCommentBlocks, blockListsPool);
            }

            Debug.Assert(texCommentBlocks.Count == 0);

            var atTexBlock = false;
            var texBlockSpanBuilder = default(TeXCommentBlockSpanBuilder);
            ITextSnapshotLine lastBlockLine = null;
            foreach (var line in snapshot.Lines)
            {
                var lineText = line.GetText();
                var numberOfWhiteSpaceCharsOnStartOfLine = lineText.NumberOfWhiteSpaceCharsOnStartOfLine();

                if (atTexBlock)
                {
                    if (lineText.StartsWith(numberOfWhiteSpaceCharsOnStartOfLine, teXCommentPrefix))
                    {
                        texBlockSpanBuilder.EndBlock(lastBlockLine);
                        texCommentBlocks.Add(texBlockSpanBuilder.Build(snapshot)); //end of current block
                        texBlockSpanBuilder = new TeXCommentBlockSpanBuilder(line.ExtentIncludingLineBreak, numberOfWhiteSpaceCharsOnStartOfLine, lineText, line.GetLineBreakText(), teXCommentPrefix, contentName); //start of new block
                        lastBlockLine = line;
                    }
                    else if (lineText.StartsWith(numberOfWhiteSpaceCharsOnStartOfLine, commentPrefix))
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
                else if (lineText.StartsWith(numberOfWhiteSpaceCharsOnStartOfLine, teXCommentPrefix))
                {
                    //start of new block
                    atTexBlock = true;
                    texBlockSpanBuilder = new TeXCommentBlockSpanBuilder(line.ExtentIncludingLineBreak, numberOfWhiteSpaceCharsOnStartOfLine, lineText, line.GetLineBreakText(), teXCommentPrefix, contentName);
                    lastBlockLine = line;
                }
            }
            if (atTexBlock)
            {
                texBlockSpanBuilder.EndBlock(lastBlockLine);
                texCommentBlocks.Add(texBlockSpanBuilder.Build(snapshot));
            }

            return new PooledStructEnumerable<TeXCommentBlockSpan>(texCommentBlocks, blockListsPool);
        }

        public PooledStructEnumerable<TeXCommentBlockSpan> GetBlocksIntersectedBy(ITextSnapshot snapshot, Span span)
        {
            var blocks = GetTexCommentBlocks(snapshot);

            var results = blockListsPool.Get();
            foreach (var block in blocks)
            {
                if (block.Span.IntersectsWith(span))
                {
                    results.Add(block);
                }
            }

            return new PooledStructEnumerable<TeXCommentBlockSpan>(results, blockListsPool);
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