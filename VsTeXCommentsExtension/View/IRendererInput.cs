using Microsoft.VisualStudio.Text.Editor;

namespace VsTeXCommentsExtension.View
{
    public interface IRendererInput
    {
        ITextView TextView { get; }
    }
}
