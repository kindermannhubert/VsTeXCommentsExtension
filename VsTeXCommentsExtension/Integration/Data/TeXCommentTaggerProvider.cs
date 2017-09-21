using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;

namespace VsTeXCommentsExtension.Integration.Data
{
    [Export(typeof(ITaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(TeXCommentTag))]
    internal sealed class TeXCommentTaggerProvider : ITaggerProvider
    {
        [Import]
        private WpfTextViewResources WpfTextViewResources = null; //MEF

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer)
            where T : ITag
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (typeof(T) != typeof(TeXCommentTag))
                throw new ArgumentNullException(nameof(T));

            return WpfTextViewResources.GetTeXCommentTagger(buffer) as ITagger<T>;
        }
    }
}