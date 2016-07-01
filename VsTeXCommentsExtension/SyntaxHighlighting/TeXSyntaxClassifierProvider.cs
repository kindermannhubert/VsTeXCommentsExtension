using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VsTeXCommentsExtension.SyntaxHighlighting
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("text")]
    internal class TeXSyntaxClassifierProvider : IClassifierProvider
    {
        [Import]
        internal IClassificationTypeRegistryService ClassificationRegistry = null; //MEF

        private static TeXSyntaxClassifier diffClassifier;

        public IClassifier GetClassifier(ITextBuffer buffer)
        {
            if (diffClassifier == null)
                diffClassifier = new TeXSyntaxClassifier(ClassificationRegistry);

            return diffClassifier;
        }
    }
}
