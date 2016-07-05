using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using System.Windows.Media;

namespace VsTeXCommentsExtension.SyntaxHighlighting
{
    internal static class ClassificationDefinitions
    {
        #region Classification Type Definitions
        [Export]
        [Name("TeX")]
        internal static ClassificationTypeDefinition TeXClassificationDefinition = null;

        [Export]
        [Name("TeX.command")]
        [BaseDefinition("TeX")]
        internal static ClassificationTypeDefinition TeXAddedDefinition = null;

        [Export]
        [Name("TeX.mathBlock")]
        [BaseDefinition("TeX")]
        internal static ClassificationTypeDefinition TeXRemovedDefinition = null;
        #endregion

        #region Classification Format Productions
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "TeX.command")]
        [Name("TeX.command")]
        [Order(After = Priority.High)]
        internal sealed class TeXAddedFormat : ClassificationFormatDefinition
        {
            public TeXAddedFormat()
            {
                ForegroundColor = Colors.Blue;
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "TeX.mathBlock")]
        [Name("TeX.mathBlock")]
        [Order(After = Priority.High)]
        internal sealed class TeXRemovedFormat : ClassificationFormatDefinition
        {
            public TeXRemovedFormat()
            {
                ForegroundColor = Colors.Red;
            }
        }
        #endregion
    }
}