using Microsoft.VisualStudio.Text.Editor;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View
{
    public interface IRendererInput
    {
        ITextView TextView { get; }
        ITagAdornment TagAdornment { get; }
    }
}
