using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
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
        private IClassifierAggregatorService AggregatorService { get; set; }

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer)
            where T : ITag
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (typeof(T) != typeof(TeXCommentTag))
                throw new ArgumentNullException(nameof(T));

            return buffer.Properties.GetOrCreateSingletonProperty(
                () =>
                {
                    var classifier = AggregatorService.GetClassifier(buffer);
                    return new TeXCommentTagger(buffer, classifier);
                }) as ITagger<T>;
        }
    }
}
