using Microsoft.VisualStudio.Text.Editor;
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
    public class HtmlRenderer : IRenderer<BitmapSource>, IDisposable
    {
        private const int CacheVersion = 2; //increase when we want to invalidate all cached results
        private const int WaitingIntervalMs = 50;
        private const int DefaultBrowserWidth = 512;
        private const int DefaultBrowserHeight = 128;

        private readonly HtmlRendererCache cache = new HtmlRendererCache();
        private readonly WebBrowser webBrowser;
        private readonly ObjectForScripting objectForScripting = new ObjectForScripting();
        private readonly Font font;

        private volatile BitmapSource resultImage;
        private volatile bool documentCompleted;
        private volatile bool mathJaxRenderingDone;
        private volatile string currentContent;

        public wpf.Color Foreground { get; set; }

        private BGR backgroundBgr;
        private wpf.Color background;
        public wpf.Color Background
        {
            get { return background; }
            set
            {
                background = value;
                backgroundBgr = new BGR { R = value.R, G = value.G, B = value.B };
            }
        }

        private double zoomScale;

        public HtmlRenderer(double zoomPercentage, wpf.Color background, wpf.Color foreground, Font font)
        {
            this.zoomScale = 0.01 * zoomPercentage;
            this.Background = background;
            this.Foreground = foreground;
            this.font = font;

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

            VisualStudioSettings.Instance.ZoomChanged += OnZoomChanged;
        }

        private void OnZoomChanged(IWpfTextView textView, double zoomPercentage)
        {
            zoomScale = 0.01 * zoomPercentage;
        }

        private void WebBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            documentCompleted = true;
        }

        public BitmapSource Render(string content)
        {
            Debug.Assert(content != null);

#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile
            var cacheInfo = new HtmlRendererCache.Info(
                                   content,
                Foreground,
                Background,
                font,
                zoomScale * ExtensionSettings.Instance.CustomZoomScale,
                CacheVersion);
            if (cache.TryGetImage(cacheInfo, out resultImage))
            {
                return resultImage;
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
            while (resultImage == null)
            {
                Thread.Sleep(WaitingIntervalMs);
            }

            return resultImage;
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
                webBrowser.Width = (int)(zoomScale * ExtensionSettings.Instance.CustomZoomScale * DefaultBrowserWidth);
                webBrowser.Height = (int)(zoomScale * ExtensionSettings.Instance.CustomZoomScale * DefaultBrowserHeight);

                const int ExtraMargin = 4;
                var myDiv = webBrowser.Document.GetElementById("myDiv");
                var width = (myDiv.OffsetRectangle.X + myDiv.ScrollRectangle.Width) + ExtraMargin;
                var height = (myDiv.OffsetRectangle.Y + myDiv.ScrollRectangle.Height) + ExtraMargin;

                webBrowser.Width = width;
                webBrowser.Height = height;

                var pixelFormat = PixelFormat.Format24bppRgb;
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

                        using (var croppedBitmap = CropToContent(bitmap, backgroundBgr))
                        {
                            var croppedBitmapData = croppedBitmap.LockBits(new Rectangle(0, 0, croppedBitmap.Width, croppedBitmap.Height), ImageLockMode.ReadOnly, croppedBitmap.PixelFormat);
                            try
                            {
                                var bitmapSource = BitmapSource.Create(
                                    croppedBitmapData.Width, croppedBitmapData.Height,
                                    Native.CurrentDpiX, Native.CurrentDpiY,
                                    wpf.PixelFormats.Bgr24, null,
                                    croppedBitmapData.Scan0,
                                    croppedBitmapData.Height * croppedBitmapData.Stride,
                                    croppedBitmapData.Stride);

                                var cacheInfo = new HtmlRendererCache.Info(
                                    currentContent,
                                    Foreground,
                                    Background,
                                    font,
                                    zoomScale * ExtensionSettings.Instance.CustomZoomScale,
                                    CacheVersion);
                                cache.Add(cacheInfo, croppedBitmap);
                                resultImage = bitmapSource;
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

        private static unsafe Bitmap CropToContent(Bitmap source, BGR background)
        {
            Debug.Assert(source.PixelFormat == PixelFormat.Format24bppRgb);

            int sourceWidth = source.Width;
            int sourceHeight = source.Height;

            int minX = int.MaxValue, maxX = int.MinValue;
            int minY = int.MaxValue, maxY = int.MinValue;
            var sourceData = source.LockBits(new Rectangle(0, 0, sourceWidth, sourceHeight), ImageLockMode.ReadOnly, source.PixelFormat);
            try
            {
                for (int y = 0; y < sourceHeight; y++)
                {
                    var pSourceData = (BGR*)((byte*)sourceData.Scan0 + y * sourceData.Stride);
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
                        var pResultData = (BGR*)((byte*)resultData.Scan0 + y * resultData.Stride);
                        var pSourceData = (BGR*)((byte*)sourceData.Scan0 + (minY + y) * sourceData.Stride);
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

        private struct BGR
        {
            public byte B, G, R;

            public static bool operator ==(BGR a, BGR b) => a.B == b.B && a.G == b.G && a.R == b.R;
            public static bool operator !=(BGR a, BGR b) => a.B != b.B || a.G != b.G || a.R != b.R;

            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (obj is BGR) return this == (BGR)obj;
                return false;
            }

            public override int GetHashCode() => B ^ G ^ R;
        }

        private string GetHtmlSource(string content)
        {
            var template = new TeXCommentHtmlTemplate()
            {
                BackgroundColor = background,
                ForegroundColor = Foreground,
                FontFamily = font.FontFamily.Name,
                FontSize = zoomScale * ExtensionSettings.Instance.CustomZoomScale * font.Size,
                Source = content
            };

            return template.TransformText();
        }

        public void Dispose()
        {
            webBrowser?.Dispose();
            VisualStudioSettings.Instance.ZoomChanged -= OnZoomChanged;
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
