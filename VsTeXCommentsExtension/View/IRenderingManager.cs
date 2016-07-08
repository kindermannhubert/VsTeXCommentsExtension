using System;

namespace VsTeXCommentsExtension.View
{
    public interface IRenderingManager : IRenderingManager<HtmlRenderer.Input, RendererResult>
    {
    }

    public interface IRenderingManager<TInput, TResult>
    {
        void RenderAsync(TInput input, Action<TResult> renderingDoneCallback);
    }
}
