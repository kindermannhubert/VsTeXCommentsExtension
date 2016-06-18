using System;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View
{
    public interface IRenderingManager : IRenderingManager<BitmapSource>
    {
    }

    public interface IRenderingManager<TResult>
    {
        void LoadContentAsync(string content, Action<TResult> renderingDoneCallback);
    }
}
