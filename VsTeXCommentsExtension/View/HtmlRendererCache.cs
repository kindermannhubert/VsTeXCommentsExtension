using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;

using wpf = System.Windows.Media;

namespace VsTeXCommentsExtension.View
{
    public class HtmlRendererCache : IDisposable
    {
        private readonly Mutex mutex = new Mutex(false, $"{nameof(VsTeXCommentsExtension)}.{nameof(HtmlRendererCache)}.Mutex");

        public string CacheDirectory { get; }

        public HtmlRendererCache()
        {
            CacheDirectory = Path.Combine(Path.GetTempPath(), nameof(VsTeXCommentsExtension), nameof(HtmlRendererCache));
            if (!Directory.Exists(CacheDirectory))
            {
                Directory.CreateDirectory(CacheDirectory);
            }
        }

        public bool TryGetImage(Info info, out RendererResult? result)
        {
            result = null;

            var filePath = Path.Combine(CacheDirectory, info.GetFileName());
            try
            {
                mutex.WaitOne();

                var filePathTxt = filePath + ".txt";
                var filePathPng = filePath + ".png";

                if (!File.Exists(filePathTxt) || !File.Exists(filePathPng)) return false;

                if (info.ToString() != File.ReadAllText(filePathTxt)) return false; //hash conflict

                var source = ResourcesManager.CreateBitmapSourceWithCurrentDpi(filePathPng);
                result = new RendererResult(source, filePathPng, Array.Empty<string>());
                return true;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public string Add(Info info, Bitmap bitmap)
        {
            var filePath = Path.Combine(CacheDirectory, info.GetFileName());

            try
            {
                mutex.WaitOne();

                using (var fs = new FileStream(filePath + ".txt", FileMode.Create))
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write(info.ToString());
                }

                var filePathPng = filePath + ".png";
                using (var fs = new FileStream(filePathPng, FileMode.Create))
                {
                    bitmap.Save(fs, ImageFormat.Png);
                }

                return filePathPng;
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public void Dispose()
        {
            mutex?.Dispose();
        }

        public struct Info
        {
            public readonly string Content;
            public readonly wpf.Color Foreground;
            public readonly wpf.Color Background;
            public readonly Font Font;
            public readonly double ZoomScale;
            public readonly int CacheVersion;

            public Info(
                string content,
                wpf.Color foreground,
                wpf.Color background,
                Font font,
                double zoomScale,
                int cacheVersion)
            {
                Content = content;
                CacheVersion = cacheVersion;
                Foreground = foreground;
                Background = background;
                Font = font;
                ZoomScale = zoomScale;
            }

            public string GetFileName()
            {
                var hash = unchecked(
                (1783 * (ulong)Content.GetHashCode()) ^
                (1777 * (ulong)Foreground.GetHashCode()) ^
                (1759 * (ulong)Background.GetHashCode()) ^
                (1753 * (ulong)Font.FontFamily.Name.GetHashCode()) ^
                (ulong)(1747 * Font.Size) ^
                (ulong)(1741 * ZoomScale));

                return hash.ToString();
            }

            public override string ToString()
            {
                return $@"{Content}
{nameof(Foreground)}: rgb({Foreground.R}, {Foreground.G}, {Foreground.B})
{nameof(Background)}: rgb({Background.R}, {Background.G}, {Background.B})
{nameof(Font)}: {Font.FontFamily.Name} (size: {Font.Size})
{nameof(ZoomScale)}: {ZoomScale}
{nameof(CacheVersion)}: {CacheVersion}";
            }
        }
    }
}