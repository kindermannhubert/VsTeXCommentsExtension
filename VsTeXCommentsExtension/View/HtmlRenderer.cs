using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

using wpf = System.Windows.Media;

namespace VsTeXCommentsExtension.View
{
    public class HtmlRenderer : IRenderer<BitmapSource>, IDisposable
    {
        private const int CacheVersion = 1; //increase when we want to invalidate cached results
        private const int WaitingIntervalMs = 50;
        private const int DefaultBrowserWidth = 2048;
        private const int DefaultBrowserHeight = 128;

        private readonly HtmlRendererCache cache = new HtmlRendererCache();
        private readonly WebBrowser webBrowser;
        private readonly ObjectForScripting objectForScripting = new ObjectForScripting();
        private readonly wpf.Color backgroundColor;
        private readonly wpf.Color foregroundColor;
        private readonly Font font;
        private readonly BGR backgroundColorBgr;
        private readonly double renderScale;

        private volatile BitmapSource resultImage;
        private volatile bool documentCompleted;
        private volatile bool mathJaxRenderingDone;
        private volatile string currentContent;

        public HtmlRenderer(double renderScale, wpf.Color backgroundColor, wpf.Color foregroundColor, Font font)
        {
            this.renderScale = renderScale;
            this.backgroundColor = backgroundColor;
            this.foregroundColor = foregroundColor;
            this.font = font;
            this.backgroundColorBgr = new BGR { R = backgroundColor.R, G = backgroundColor.G, B = backgroundColor.B };

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

        public BitmapSource Render(string content)
        {
            Debug.Assert(content != null);

#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile
            if (cache.TryGetImage(content, CacheVersion, out resultImage))
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
                webBrowser.Width = DefaultBrowserWidth;
                webBrowser.Height = DefaultBrowserHeight;

                const int ExtraMargin = 4;
                var myDiv = webBrowser.Document.GetElementById("myDiv");
                var width = myDiv.OffsetRectangle.X + myDiv.ScrollRectangle.Width + ExtraMargin;
                var height = myDiv.OffsetRectangle.Y + myDiv.ScrollRectangle.Height + ExtraMargin;

                webBrowser.Width = width;
                webBrowser.Height = height;

                var pixelFormat = PixelFormat.Format24bppRgb;
                using (var bitmap = new Bitmap(width, height, pixelFormat))
                {
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

                        using (var croppedBitmap = CropToContent(bitmap, backgroundColorBgr))
                        {
                            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                                                    croppedBitmap.GetHbitmap(),
                                                                    IntPtr.Zero,
                                                                    Int32Rect.Empty,
                                                                    BitmapSizeOptions.FromEmptyOptions());

                            cache.Add(currentContent, CacheVersion, croppedBitmap);
                            resultImage = bitmapSource;
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
                BackgroundColor = backgroundColor,
                ForegroundColor = foregroundColor,
                FontFamily = font.FontFamily.Name,
                FontSize = renderScale * font.Size,
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
    }
}
