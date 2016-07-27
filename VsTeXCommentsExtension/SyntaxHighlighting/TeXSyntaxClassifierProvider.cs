using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using VsTeXCommentsExtension.Integration;

namespace VsTeXCommentsExtension.SyntaxHighlighting
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("CSharp")]
    internal class TeXSyntaxClassifierProvider : IClassifierProvider
    {
        [Import]
        private WpfTextViewResources WpfTextViewResources = null; //MEF

        public IClassifier GetClassifier(ITextBuffer buffer) => WpfTextViewResources.GetTeXSyntaxClassifier(buffer);
    }
}
