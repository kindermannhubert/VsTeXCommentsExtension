using System;

namespace VsTeXCommentsExtension.View
{
    public interface IRenderingManager : IRenderingManager<RendererResult>
    {
    }

    public interface IRenderingManager<TResult>
    {
        void LoadContentAsync(string content, Action<TResult> renderingDoneCallback);
    }
}
