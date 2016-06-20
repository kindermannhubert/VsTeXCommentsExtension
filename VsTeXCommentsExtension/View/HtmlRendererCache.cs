using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View
{
    public class HtmlRendererCache : IDisposable
    {
        private readonly string directory;
        private readonly Mutex mutex = new Mutex(false, $"{nameof(VsTeXCommentsExtension)}.{nameof(HtmlRendererCache)}.Mutex");

        public HtmlRendererCache()
        {
            directory = Path.Combine(Path.GetTempPath(), nameof(VsTeXCommentsExtension), nameof(HtmlRendererCache));
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        public bool TryGetImage(string content, int version, out BitmapSource bitmapSource)
        {
            bitmapSource = null;

            var filePath = Path.Combine(directory, unchecked((uint)content.GetHashCode()).ToString());
            try
            {
                mutex.WaitOne();

                var filePathTxt = filePath + ".txt";
                var filePathPng = filePath + ".png";

                if (!File.Exists(filePathTxt) || !File.Exists(filePathPng)) return false;

                if (content + version != File.ReadAllText(filePathTxt)) return false; //hash conflict

                using (var fs = new FileStream(filePathPng, FileMode.Open))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = fs;
                    bmp.EndInit();
                    bmp.Freeze();

                    bitmapSource = bmp;
                    return true;
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        public void Add(string content, int version, Bitmap bitmap)
        {

            var filePath = Path.Combine(directory, unchecked((uint)content.GetHashCode()).ToString());

            try
            {
                mutex.WaitOne();

                using (var fs = new FileStream(filePath + ".txt", FileMode.Create))
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write(content);
                    writer.Write(version);
                }

                using (var fs = new FileStream(filePath + ".png", FileMode.Create))
                {
                    bitmap.Save(fs, ImageFormat.Png);
                }
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
    }
}
