using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace VsTeXCommentsExtension.Integration
{
    internal class TextSnapshotTeXCommentBlocks
    {
        public const string CommentPrefixCSharp = "//"; 
        public const string TeXCommentPrefixCSharp = "//tex:";
        public const string CommentPrefixBasic = "'";
        public const string TeXCommentPrefixBasic = "'tex:";

        public static readonly char[] WhiteSpaces = new char[] { ' ', '\t' };

        private readonly TextSnapshotValuesPerVersionCache<PooledStructEnumerable<TeXCommentBlockSpan>> blocksPerVersion;

        private readonly ObjectPool<List<TeXCommentBlockSpan>> blockListsPool = new ObjectPool<List<TeXCommentBlockSpan>>(() => new List<TeXCommentBlockSpan>());
        private readonly ObjectPool<List<SnapshotSpan>> snapshotSpansListsPool = new ObjectPool<List<SnapshotSpan>>(() => new List<SnapshotSpan>());

        public TextSnapshotTeXCommentBlocks() 
        {
            blocksPerVersion = new TextSnapshotValuesPerVersionCache<PooledStructEnumerable<TeXCommentBlockSpan>>(GenerateTexCommentBlocks);
        }



        public StructEnumerable<TeXCommentBlockSpan> GetTexCommentBlocks(ITextSnapshot snapshot) => blocksPerVersion.GetValue(snapshot);

        private PooledStructEnumerable<TeXCommentBlockSpan> GenerateTexCommentBlocks(ITextSnapshot snapshot)
        {
            string CommentPrefix = CommentPrefixCSharp;
            string TeXCommentPrefix = TeXCommentPrefixCSharp;

            if (snapshot.ContentType.TypeName == "Basic")
            {
                CommentPrefix = CommentPrefixBasic;
                TeXCommentPrefix = TeXCommentPrefixBasic;
            }

            var texCommentBlocks = blockListsPool.Get();
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
                    if (lineText.StartsWith(numberOfWhiteSpaceCharsOnStartOfLine, TeXCommentPrefix))
                    {
                        texBlockSpanBuilder.EndBlock(lastBlockLine);
                        texCommentBlocks.Add(texBlockSpanBuilder.Build(snapshot)); //end of current block

                        texBlockSpanBuilder = new TeXCommentBlockSpanBuilder(line.ExtentIncludingLineBreak, numberOfWhiteSpaceCharsOnStartOfLine, lineText, line.GetLineBreakText(), snapshot.ContentType.TypeName); //start of new block
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
                    texBlockSpanBuilder = new TeXCommentBlockSpanBuilder(line.ExtentIncludingLineBreak, numberOfWhiteSpaceCharsOnStartOfLine, lineText, line.GetLineBreakText(), snapshot.ContentType.TypeName);
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
