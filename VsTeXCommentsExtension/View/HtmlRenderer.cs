using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

using wpf = System.Windows.Media;

namespace VsTeXCommentsExtension.View
{
    public class HtmlRenderer : IRenderer<RendererResult>, IDisposable
    {
        private const int CacheVersion = 2; //increase when we want to invalidate all cached results
        private const int WaitingIntervalMs = 50;
        private const int DefaultBrowserWidth = 512;
        private const int DefaultBrowserHeight = 128;

        private readonly HtmlRendererCache cache = new HtmlRendererCache();
        private readonly WebBrowser webBrowser;
        private readonly ObjectForScripting objectForScripting = new ObjectForScripting();
        private readonly Font font;

        private RendererResult? resultImage;
        private volatile bool documentCompleted;
        private volatile bool mathJaxRenderingDone;
        private volatile string currentContent;

        public wpf.Color Foreground { get; set; }

        private BGRA backgroundBgra;
        private wpf.Color background;
        public wpf.Color Background
        {
            get { return background; }
            set
            {
                background = value;
                backgroundBgra = new BGRA { R = value.R, G = value.G, B = value.B, A = value.A };
            }
        }

        public double ZoomScale { get; set; }

        public HtmlRenderer(double zoomPercentage, wpf.Color background, wpf.Color foreground, Font font)
        {
            this.font = font;
            ZoomScale = 0.01 * zoomPercentage;
            Background = background;
            Foreground = foreground;

            webBrowser = new WebBrowser()
            {
                Width = DefaultBrowserWidth,
                Height = DefaultBrowserHeight,
                Margin = new Padding(0, 0, 0, 0),
                ScrollBarsEnabled = false,
                ScriptErrorsSuppressed = true,
                ObjectForScripting = objectForScripting
            };
            webBrowser.DocumentCompleted += WebBrowser_DocumentCompleted;
            webBrowser.Navigate("about:blank");
            webBrowser.Document.OpenNew(true);

            objectForScripting.RenderingDone += () => mathJaxRenderingDone = true;
        }

        private void WebBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            documentCompleted = true;
        }

        public RendererResult Render(string content)
        {
            Debug.Assert(content != null);

#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile
            var cacheInfo = new HtmlRendererCache.Info(
                                   content,
                Foreground,
                Background,
                font,
                ZoomScale * ExtensionSettings.Instance.CustomZoomScale,
                CacheVersion);
            if (cache.TryGetImage(cacheInfo, out resultImage))
            {
                return resultImage.Value;
            }
#pragma warning restore CS0420 // A reference to a volatile field will not be treated as volatile

            documentCompleted = false;
            mathJaxRenderingDone = false;
            resultImage = null;
            webBrowser.DocumentText = GetHtmlSource(content);
            currentContent = content;

            //wait until document is loaded
            while (!documentCompleted)
            {
                Thread.Sleep(WaitingIntervalMs);
            }

            RenderInternal();

            //wait until result image is ready
            while (!resultImage.HasValue)
            {
                Thread.Sleep(WaitingIntervalMs);
            }

            return resultImage.Value;
        }

        private unsafe void RenderInternal()
        {
            if (webBrowser.InvokeRequired)
            {
                //wait until MathJax is done with rendering
                while (!mathJaxRenderingDone)
                {
                    Thread.Sleep(WaitingIntervalMs);
                }
                webBrowser.Invoke(new Action(RenderInternal));
            }
            else
            {
                webBrowser.Width = (int)(ZoomScale * ExtensionSettings.Instance.CustomZoomScale * DefaultBrowserWidth);
                webBrowser.Height = (int)(ZoomScale * ExtensionSettings.Instance.CustomZoomScale * DefaultBrowserHeight);

                const int ExtraMargin = 4;
                var myDiv = webBrowser.Document.GetElementById("myDiv");
                var width = (myDiv.OffsetRectangle.X + myDiv.ScrollRectangle.Width) + ExtraMargin;
                var height = (myDiv.OffsetRectangle.Y + myDiv.ScrollRectangle.Height) + ExtraMargin;

                webBrowser.Width = width;
                webBrowser.Height = height;
                webBrowser.Document.BackColor = Color.Transparent;

                const PixelFormat pixelFormat = PixelFormat.Format32bppArgb;
                using (var bitmap = new Bitmap(width, height, pixelFormat))
                {
                    bitmap.SetResolution(Native.CurrentDpiX, Native.CurrentDpiY);
                    var viewObject = webBrowser.Document.DomDocument as IViewObject;

                    if (viewObject != null)
                    {
                        var webBrowserRectangle = new tagRECT
                        {
                            left = myDiv.OffsetRectangle.Left,
                            top = myDiv.OffsetRectangle.Top,
                            right = myDiv.OffsetRectangle.Right,
                            bottom = myDiv.OffsetRectangle.Bottom
                        };

                        var bitmapRectangle = new tagRECT
                        {
                            left = 0,
                            top = 0,
                            right = width,
                            bottom = height
                        };

                        using (var gr = Graphics.FromImage(bitmap))
                        {
                            var hdc = gr.GetHdc();

                            try
                            {
                                int hr = viewObject.Draw(1 /*DVASPECT_CONTENT*/,
                                                -1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                                                hdc,
                                                ref bitmapRectangle,
                                                ref webBrowserRectangle,
                                                IntPtr.Zero, 0);
                            }
                            finally
                            {
                                gr.ReleaseHdc();
                            }
                        }

                        using (var croppedBitmap = CropToContent(bitmap, backgroundBgra))
                        {
                            MakeBackgroundTransparent(croppedBitmap, backgroundBgra);
                            var croppedBitmapData = croppedBitmap.LockBits(new Rectangle(0, 0, croppedBitmap.Width, croppedBitmap.Height), ImageLockMode.ReadOnly, croppedBitmap.PixelFormat);
                            try
                            {
                                var bitmapSource = BitmapSource.Create(
                                    croppedBitmapData.Width, croppedBitmapData.Height,
                                    Native.CurrentDpiX, Native.CurrentDpiY,
                                    wpf.PixelFormats.Bgra32, null,
                                    croppedBitmapData.Scan0,
                                    croppedBitmapData.Height * croppedBitmapData.Stride,
                                    croppedBitmapData.Stride);

                                var cacheInfo = new HtmlRendererCache.Info(
                                    currentContent,
                                    Foreground,
                                    Background,
                                    font,
                                    ZoomScale * ExtensionSettings.Instance.CustomZoomScale,
                                    CacheVersion);

                                var cachedImagePath = cache.Add(cacheInfo, croppedBitmap);
                                resultImage = new RendererResult(bitmapSource, cachedImagePath);
                            }
                            finally
                            {
                                croppedBitmap.UnlockBits(croppedBitmapData);
                            }
                        }
                    }
                }
            }
        }

        private static unsafe Bitmap CropToContent(Bitmap source, BGRA background)
        {
            Debug.Assert(source.PixelFormat == PixelFormat.Format32bppArgb);

            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            var sourceData = source.LockBits(new Rectangle(0, 0, sourceWidth, sourceHeight), ImageLockMode.ReadOnly, source.PixelFormat);
            try
            {
                for (int y = 0; y < sourceHeight; y++)
                {
                    var pSourceData = (BGRA*)((byte*)sourceData.Scan0 + y * sourceData.Stride);
                    for (int x = 0; x < sourceWidth; x++)
                    {
                        var col = pSourceData[x];
                        if (col != background)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }
                    }
                }

                if (minX == int.MaxValue)
                {
                    //no content found
                    var bmp = new Bitmap(1, 1, source.PixelFormat);
                    bmp.SetPixel(0, 0, Color.FromArgb(background.R, background.G, background.B));
                    return bmp;
                }

                int resultWidth = maxX - minX + 1;
                int resultHeight = maxY - minY + 1;
                var result = new Bitmap(resultWidth, resultHeight, source.PixelFormat);
                var resultData = result.LockBits(new Rectangle(0, 0, resultWidth, resultHeight), ImageLockMode.WriteOnly, result.PixelFormat);
                try
                {
                    for (int y = 0; y < resultHeight; y++)
                    {
                        var pResultData = (BGRA*)((byte*)resultData.Scan0 + y * resultData.Stride);
                        var pSourceData = (BGRA*)((byte*)sourceData.Scan0 + (minY + y) * sourceData.Stride);
                        for (int x = 0; x < resultWidth; x++)
                        {
                            pResultData[x] = pSourceData[minX + x];
                        }
                    }
                }
                finally
                {
                    result.UnlockBits(sourceData);
                }

                return result;
            }
            finally
            {
                source.UnlockBits(sourceData);
            }
        }

        private static unsafe void MakeBackgroundTransparent(Bitmap bitmap, BGRA background)
        {
            Debug.Assert(bitmap.PixelFormat == PixelFormat.Format32bppArgb);

            int width = bitmap.Width;
            int height = bitmap.Height;

            var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, bitmap.PixelFormat);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    var pData = (BGRA*)((byte*)data.Scan0 + y * data.Stride);
                    for (int x = 0; x < width; x++)
                    {
                        var col = pData[x];

                        if (col == background)
                        {
                            *(int*)(pData + x) = 0; //transparent
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private struct BGRA
        {
            public byte B, G, R, A;

            public static bool operator ==(BGRA a, BGRA b) => a.B == b.B && a.G == b.G && a.R == b.R && a.A == b.A;
            public static bool operator !=(BGRA a, BGRA b) => a.B != b.B || a.G != b.G || a.R != b.R || a.A != b.A;

            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (obj is BGRA) return this == (BGRA)obj;
                return false;
            }

            public override int GetHashCode() => B ^ G ^ R ^ A;
        }

        private string GetHtmlSource(string content)
        {
            var template = new TeXCommentHtmlTemplate()
            {
                BackgroundColor = background,
                ForegroundColor = Foreground,
                FontFamily = font.FontFamily.Name,
                FontSize = ZoomScale * ExtensionSettings.Instance.CustomZoomScale * font.Size,
                Source = content
            };

            return template.TransformText();
        }

        public void Dispose()
        {
            webBrowser?.Dispose();
        }

        [ComImport]
        [ComVisible(true)]
        [Guid("0000010d-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IViewObject
        {
            [return: MarshalAs(UnmanagedType.I4)]
            [PreserveSig]
            int Draw(
                [MarshalAs(UnmanagedType.U4)] uint dwDrawAspect,
                int lindex,
                IntPtr pvAspect,
                [In] IntPtr ptd,
                IntPtr hdcTargetDev, IntPtr hdcDraw,
                [MarshalAs(UnmanagedType.Struct)] ref tagRECT lprcBounds,
                [MarshalAs(UnmanagedType.Struct)] ref tagRECT lprcWBounds,
                IntPtr pfnContinue,
                [MarshalAs(UnmanagedType.U4)] uint dwContinue);
        }

        private struct tagRECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [ComVisible(true)]
        public class ObjectForScripting
        {
            public void RaiseRenderingDone()
            {
                RenderingDone?.Invoke();
            }

            public event Action RenderingDone;
        }

        private class Native
        {
            private static int dpiX = -1;
            private static int dpiY = -1;

            [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true, ExactSpelling = true)]
            private static extern int GetDeviceCaps(IntPtr hDC, int nIndex);

            private enum DeviceCap
            {
                LOGPIXELSX = 88,
                LOGPIXELSY = 90
            }

            private static void Init()
            {
                if (dpiX == -1)
                {
                    using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        var desktop = g.GetHdc();
                        dpiX = GetDeviceCaps(desktop, (int)DeviceCap.LOGPIXELSX);
                        dpiY = GetDeviceCaps(desktop, (int)DeviceCap.LOGPIXELSY);
                        g.ReleaseHdc();
                    }
                }
            }

            public static int CurrentDpiX
            {
                get
                {
                    Init();
                    return dpiX;
                }
            }

            public static int CurrentDpiY
            {
                get
                {
                    Init();
                    return dpiY;
                }
            }
        }
    }
}
