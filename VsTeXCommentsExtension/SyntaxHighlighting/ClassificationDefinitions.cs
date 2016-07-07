using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace VsTeXCommentsExtension.SyntaxHighlighting
{
    internal static class ClassificationDefinitions
    {
        #region Classification Type Definitions
        [Export]
        [Name("TeX.Command")]
        [BaseDefinition(PredefinedClassificationTypeNames.Keyword)]
        internal static ClassificationTypeDefinition TeXCommandDefinition = null;

        [Export]
        [Name("TeX.MathBlock")]
        [BaseDefinition(PredefinedClassificationTypeNames.String)]
        internal static ClassificationTypeDefinition TeXMathBlockDefinition = null;
        #endregion

        #region Classification Format Productions
        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "TeX.Command")]
        [Name("TeX.Command")]
        [Order(After = Priority.High)]
        [UserVisible(true)]
        internal sealed class TeXCommandFormat : ClassificationFormatDefinition
        {
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = "TeX.MathBlock")]
        [Name("TeX.MathBlock")]
        [Order(After = Priority.High)]
        [UserVisible(true)]
        internal sealed class TeXMathBlockFormat : ClassificationFormatDefinition
        {
        }
        #endregion
    }
}