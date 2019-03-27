using System;
using Microsoft.VisualStudio.Text.Editor;

namespace VsTeXCommentsExtension.View
{
    public interface IRenderingManager : IRenderingManager<HtmlRenderer.Input, RendererResult>
    {
    }

    public interface IRenderingManager<TInput, TResult>
        where TInput : IRendererInput
    {
        void RenderAsync(TInput input, Action<TResult> renderingDoneCallback);
        void DiscartRenderingRequestsForTextView(ITextView textView);
    }
}