using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.Text.Editor;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.Integration.View;
using wpf = System.Windows.Media;

namespace VsTeXCommentsExtension.View
{
    public class HtmlRenderer : IRenderer<HtmlRenderer.Input, RendererResult>, IDisposable
    {
        private const int CacheVersion = 2; //increase when we want to invalidate all cached results
        private const int WaitingIntervalMs = 50;
        private const int DefaultBrowserWidth = 512;
        private const int DefaultBrowserHeight = 128;

        private readonly HtmlRendererCache cache = new HtmlRendererCache();
        private readonly WebBrowser webBrowser;
        private readonly ObjectForScripting objectForScripting = new ObjectForScripting();

        private RendererResult? rendererResult;
        private volatile bool documentCompleted;
        private volatile bool mathJaxRenderingDone;
        private volatile string currentContent;

        public HtmlRenderer()
        {
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

        public RendererResult Render(Input input)
        {
#pragma warning disable CS0420 // A reference to a volatile field will not be treated as volatile
            var cacheInfo = new HtmlRendererCache.Info(
                input.Content,
                input.Foreground,
                input.Background,
                input.Font,
                input.ZoomScale * ExtensionSettings.Instance.CustomZoomScale,
                CacheVersion);
            if (cache.TryGetImage(cacheInfo, out rendererResult))
            {
                return rendererResult.Value;
            }
#pragma warning restore CS0420 // A reference to a volatile field will not be treated as volatile

            documentCompleted = false;
            mathJaxRenderingDone = false;
            objectForScripting.ClearErrors();
            rendererResult = null;
            webBrowser.DocumentText = GetHtmlSource(input);
            currentContent = input.Content;

            //wait until document is loaded
            while (!documentCompleted)
            {
                Thread.Sleep(WaitingIntervalMs);
            }

            RenderInternal(input);

            //wait until result image is ready
            while (!rendererResult.HasValue)
            {
                Thread.Sleep(WaitingIntervalMs);
            }

            return rendererResult.Value;
        }

        private unsafe void RenderInternal(Input input)
        {
            if (webBrowser.InvokeRequired)
            {
                //wait until MathJax is done with rendering
                while (!mathJaxRenderingDone)
                {
                    Thread.Sleep(WaitingIntervalMs);
                }
                webBrowser.Invoke(new Action<Input>(RenderInternal), input);
            }
            else
            {
                webBrowser.Width = (int)(input.ZoomScale * ExtensionSettings.Instance.CustomZoomScale * DefaultBrowserWidth);
                webBrowser.Height = (int)(input.ZoomScale * ExtensionSettings.Instance.CustomZoomScale * DefaultBrowserHeight);

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
                    if (webBrowser.Document.DomDocument is IViewObject viewObject)
                    {
                        var webBrowserRectangle = new TagRECT(myDiv.OffsetRectangle);
                        var bitmapRectangle = new TagRECT(0, 0, width, height);

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

                        var background = BGRA.From(input.Background);
                        using (var croppedBitmap = CropToContent(bitmap, background))
                        {
                            MakeBackgroundTransparent(croppedBitmap, background);

                            var bitmapSource = ResourcesManager.CreateBitmapSourceWithCurrentDpi(croppedBitmap);

                            var cacheInfo = new HtmlRendererCache.Info(
                                currentContent,
                                input.Foreground,
                                input.Background,
                                input.Font,
                                input.ZoomScale * ExtensionSettings.Instance.CustomZoomScale,
                                CacheVersion);

                            var errors = objectForScripting.Errors.Count == 0 ? Array.Empty<string>() : objectForScripting.Errors.ToArray();
                            var cachedImagePath = errors.Length == 0 ? cache.Add(cacheInfo, croppedBitmap) : null;
                            rendererResult = new RendererResult(bitmapSource, cachedImagePath, errors);
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

        private string GetHtmlSource(Input input)
        {
            var template = new TeXCommentHtmlTemplate()
            {
                BackgroundColor = input.Background,
                ForegroundColor = input.Foreground,
                FontFamily = input.Font.FontFamily.Name,
                FontSize = input.ZoomScale * ExtensionSettings.Instance.CustomZoomScale * input.Font.Size,
                Source = input.Content
            };

            return template.TransformText();
        }

        public void Dispose()
        {
            webBrowser?.Dispose();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0)]
        private readonly struct BGRA
        {
            public readonly byte B, G, R, A;

            public static bool operator ==(BGRA a, BGRA b) => a.B == b.B && a.G == b.G && a.R == b.R && a.A == b.A;
            public static bool operator !=(BGRA a, BGRA b) => a.B != b.B || a.G != b.G || a.R != b.R || a.A != b.A;

            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                if (obj is BGRA) return this == (BGRA)obj;
                return false;
            }

            public override int GetHashCode() => B ^ G ^ R ^ A;

            public static unsafe BGRA From(wpf.Color color)
            {
                var p = stackalloc byte[4];
                p[0] = color.B;
                p[1] = color.G;
                p[2] = color.R;
                p[3] = color.A;
                return *(BGRA*)p;
            }
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
                [MarshalAs(UnmanagedType.Struct)] ref TagRECT lprcBounds,
                [MarshalAs(UnmanagedType.Struct)] ref TagRECT lprcWBounds,
                IntPtr pfnContinue,
                [MarshalAs(UnmanagedType.U4)] uint dwContinue);
        }

        private readonly struct TagRECT
        {
            public readonly int Left;
            public readonly int Top;
            public readonly int Right;
            public readonly int Bottom;

            public TagRECT(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public TagRECT(Rectangle rectangle)
                : this(rectangle.Left, rectangle.Top, rectangle.Right, rectangle.Bottom)
            { }
        }

        [ComVisible(true)]
        public class ObjectForScripting
        {
            private readonly List<string> errors = new List<string>();

            public void AddError(string message)
            {
                errors.Add(message);
            }

            public void ClearErrors()
            {
                errors.Clear();
            }

            public void RaiseRenderingDone()
            {
                RenderingDone?.Invoke();
            }

            public event Action RenderingDone;

            public IReadOnlyList<string> Errors => errors;
        }

        public readonly struct Input : IRendererInput
        {
            public readonly string Content;
            public readonly double ZoomScale;
            public readonly wpf.Color Foreground;
            public readonly wpf.Color Background;
            public readonly Font Font;
            public ITextView TextView { get; }
            public ITagAdornment TagAdornment { get; }

            public Input(TeXCommentTag dataTag, double zoomScale, wpf.Color foreground, wpf.Color background, Font font, ITextView textView, ITagAdornment tagAdornment)
            {
                Content = dataTag.GetTextWithoutCommentMarks();
                ZoomScale = zoomScale;
                Foreground = foreground;
                Background = background;
                Font = font;
                TextView = textView;
                TagAdornment = tagAdornment;
            }
        }
    }
}