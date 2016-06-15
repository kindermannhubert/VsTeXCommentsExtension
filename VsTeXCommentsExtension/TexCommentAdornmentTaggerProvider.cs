using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace VsTeXCommentsExtension
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [ContentType("projection")]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class TeXCommentAdornmentTaggerProvider : IViewTaggerProvider
    {
#pragma warning disable 649 // "field never assigned to" -- field is set by MEF.
        [Import]
        internal IBufferTagAggregatorFactoryService BufferTagAggregatorFactoryService;
#pragma warning restore 649

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView == null)
                throw new ArgumentNullException("textView");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (buffer != textView.TextBuffer)
                return null;

            return TeXCommentAdornmentTagger.GetTagger(
                (IWpfTextView)textView,
                new Lazy<ITagAggregator<TeXCommentTag>>(
                    () => BufferTagAggregatorFactoryService.CreateTagAggregator<TeXCommentTag>(textView.TextBuffer)))
                as ITagger<T>;
        }
    }
}
