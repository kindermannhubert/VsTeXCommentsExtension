using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View
{
    public class ResourcesManager : INotifyPropertyChanged, IDisposable, IResourcesManager
    {
        private static readonly Color ForegroundUIColor_Dark = Color.FromRgb(64, 64, 64);
        private static readonly Color ForegroundUIColor_Light = Color.FromRgb(243, 243, 243);
        private static readonly Color BackgroundUIColor_Dark = ForegroundUIColor_Light;
        private static readonly Color BackgroundUIColor_Light = ForegroundUIColor_Dark;

        private static readonly ImageSource dropDown_Light = new BitmapImage(GetAssemblyResourceUri("DropDown_Light.png"));
        private static readonly ImageSource dropDown_Dark = new BitmapImage(GetAssemblyResourceUri("DropDown_Dark.png"));

        private static readonly ImageSource edit_Light = new BitmapImage(GetAssemblyResourceUri("Edit_Light.png"));
        private static readonly ImageSource edit_Dark = new BitmapImage(GetAssemblyResourceUri("Edit_Dark.png"));

        private static readonly ImageSource show_Light = new BitmapImage(GetAssemblyResourceUri("Show_Light.png"));
        private static readonly ImageSource show_Dark = new BitmapImage(GetAssemblyResourceUri("Show_Dark.png"));

        private static readonly ImageSource warning_Light = new BitmapImage(GetAssemblyResourceUri("Warning_Light.png"));
        private static readonly ImageSource warning_Dark = new BitmapImage(GetAssemblyResourceUri("Warning_Dark.png"));

        private static readonly Dictionary<IWpfTextView, ResourcesManager> instances = new Dictionary<IWpfTextView, ResourcesManager>();

        public static int CurrentDpiX => Native.CurrentDpiX;
        public static int CurrentDpiY => Native.CurrentDpiY;

        public static ResourcesManager GetOrCreate(IWpfTextView textView)
        {
            lock (instances)
            {
                ResourcesManager instance;
                if (!instances.TryGetValue(textView, out instance))
                {
                    instance = new ResourcesManager(textView);
                    instances.Add(textView, instance);
                }
                return instance;
            }
        }

        private readonly VsSettings vsSettings;
        private bool useDark = true;

        public ImageSource DropDown => useDark ? dropDown_Dark : dropDown_Light;

        public ImageSource Edit => useDark ? edit_Dark : edit_Light;

        public ImageSource Show => useDark ? show_Dark : show_Light;

        public ImageSource Warning => useDark ? warning_Dark : warning_Light;

        public Color ForegroundUIColor => useDark ? ForegroundUIColor_Dark : ForegroundUIColor_Light;

        public Color BackgroundUIColor => useDark ? BackgroundUIColor_Dark : BackgroundUIColor_Light;

        public event PropertyChangedEventHandler PropertyChanged;

        private ResourcesManager(IWpfTextView textView)
        {
            vsSettings = VsSettings.GetOrCreate(textView);
            OnEditorBackgroundColorChange(vsSettings.CommentsBackground.Color);
            vsSettings.CommentsColorChanged += CommentsColorChanged;
        }

        private void CommentsColorChanged(IWpfTextView textView, SolidColorBrush foreground, SolidColorBrush background)
        {
            OnEditorBackgroundColorChange(background.Color);
        }

        private Color editorBackgroundColor;
        private void OnEditorBackgroundColorChange(Color color)
        {
            if (editorBackgroundColor != color)
            {
                editorBackgroundColor = color;
                var useDarkNew = color.R + color.G + color.B > 3 * 127.5;
                if (useDark != useDarkNew)
                {
                    useDark = useDarkNew;

                    OnPropertyChanged(nameof(DropDown));
                    OnPropertyChanged(nameof(Edit));
                    OnPropertyChanged(nameof(Show));
                    OnPropertyChanged(nameof(Warning));
                    OnPropertyChanged(nameof(ForegroundUIColor));
                    OnPropertyChanged(nameof(BackgroundUIColor));
                }
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            vsSettings.CommentsColorChanged -= CommentsColorChanged;
        }

        public static Uri GetAssemblyResourceUri(string pathRelativeToResourcesFolder)
        {
            return new Uri($"pack://application:,,,/VsTeXCommentsExtension;component/Resources/{pathRelativeToResourcesFolder}");
        }

        public static unsafe BitmapSource CreateBitmapSourceWithCurrentDpi(System.Drawing.Bitmap bitmap)
        {
            Debug.Assert(bitmap.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                bitmap.SetResolution(CurrentDpiX, CurrentDpiY); //maybe not necessary
                var source = BitmapSource.Create(
                              bitmapData.Width, bitmapData.Height,
                              CurrentDpiX, CurrentDpiY,
                              PixelFormats.Bgra32, null,
                              bitmapData.Scan0,
                              bitmapData.Height * bitmapData.Stride,
                              bitmapData.Stride);

                source.Freeze();
                return source;
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }

        public static unsafe BitmapSource CreateBitmapSourceWithCurrentDpi(string path)
        {
            using (var bmp = new System.Drawing.Bitmap(path))
            {
                return CreateBitmapSourceWithCurrentDpi(bmp);
            }
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
                    using (var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero))
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
