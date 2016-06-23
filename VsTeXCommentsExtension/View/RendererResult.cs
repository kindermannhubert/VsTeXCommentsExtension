using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View
{
    public struct RendererResult
    {
        public readonly BitmapSource Image;
        public readonly string CachePath;

        public RendererResult(BitmapSource image, string cachePath)
        {
            Image = image;
            CachePath = cachePath;
        }
    }
}
