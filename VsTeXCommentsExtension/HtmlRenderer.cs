using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension
{
    public class HtmlRenderer : IDisposable
    {
        private readonly WebBrowser webBrowser = new WebBrowser();
        private readonly ObjectForScripting objectForScripting = new ObjectForScripting();
        private readonly BGR backgroundColor;
        private bool imageReady;

        public event EventHandler<BitmapSource> WebBrowserImageReady = null;

        public HtmlRenderer(System.Windows.Media.Color backgroundColor)
        {
            this.backgroundColor = new BGR { R = backgroundColor.R, G = backgroundColor.G, B = backgroundColor.B };

            webBrowser.DocumentCompleted += DocumentCompleted;
            webBrowser.Width = 1000;
            webBrowser.Height = 1000;
            webBrowser.ScriptErrorsSuppressed = true;
            webBrowser.ObjectForScripting = objectForScripting;
            webBrowser.Navigate("about:blank");
            webBrowser.Document.OpenNew(true);
            objectForScripting.RenderingDone += () => imageReady = true;
        }

        public void LoadContent(string content)
        {
            imageReady = false;
            webBrowser.DocumentText = content;
        }

        private void DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            Task.Run(new Action(Render));
        }

        private unsafe void Render()
        {
            if (webBrowser.InvokeRequired)
            {
                while (!imageReady)
                {
                    Thread.Sleep(50);
                }
                webBrowser.Invoke(new Action(Render));
            }
            else
            {
                const int Margin = 8;

                var myDiv = webBrowser.Document.GetElementById("myDiv");
                var width = myDiv.ClientRectangle.Width + 2 * Margin;
                var height = myDiv.ClientRectangle.Height + 2 * Margin;

                var text = webBrowser.DocumentText ?? string.Empty;
                var pixelFormat = PixelFormat.Format24bppRgb;

                using (var bitmap = new Bitmap(width, height, pixelFormat))
                {
                    var viewObject = webBrowser.Document.DomDocument as IViewObject;

                    if (viewObject != null)
                    {
                        var sourceRect = new tagRECT();
                        sourceRect.left = 0;
                        sourceRect.top = 0;
                        sourceRect.right = width;
                        sourceRect.bottom = height;

                        var targetRect = new tagRECT();
                        targetRect.left = 0;
                        targetRect.top = 0;
                        targetRect.right = webBrowser.Width;
                        targetRect.bottom = webBrowser.Height;

                        using (var gr = Graphics.FromImage(bitmap))
                        {
                            var hdc = gr.GetHdc();

                            try
                            {
                                int hr = viewObject.Draw(1 /*DVASPECT_CONTENT*/,
                                                (int)-1,
                                                IntPtr.Zero,
                                                IntPtr.Zero,
                                                IntPtr.Zero,
                                                hdc,
                                                ref targetRect,
                                                ref sourceRect,
                                                IntPtr.Zero,
                                                (uint)0);
                            }
                            finally
                            {
                                gr.ReleaseHdc();
                            }
                        }

                        //var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadWrite, pixelFormat);
                        //for (int y = 0; y < height; y++)
                        //{
                        //    var pData = (BGRA*)((byte*)data.Scan0 + y * data.Stride);
                        //    for (int x = 0; x < width; x++)
                        //    {
                        //        var col = pData[x];
                        //        var gray = (byte)((col.B + col.G + col.R) / 3);
                        //        pData[x] = new BGRA { A = (byte)(255 - gray), B = gray, G = gray, R = gray };
                        //    }
                        //}
                        //bitmap.UnlockBits(data);

                        using (var croppedBitmap = CropToContent(bitmap, backgroundColor))
                        {
                            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                                                                    croppedBitmap.GetHbitmap(),
                                                                    IntPtr.Zero,
                                                                    Int32Rect.Empty,
                                                                    BitmapSizeOptions.FromEmptyOptions());

                            WebBrowserImageReady?.Invoke(this, bitmapSource);
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

        public void Dispose()
        {
            webBrowser?.Dispose();
            WebBrowserImageReady = null;
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
                ////tagDVASPECT                
                [MarshalAs(UnmanagedType.U4)] uint dwDrawAspect,
                int lindex,
                IntPtr pvAspect,
                [In] IntPtr ptd,
                //// [MarshalAs(UnmanagedType.Struct)] ref DVTARGETDEVICE ptd,
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
