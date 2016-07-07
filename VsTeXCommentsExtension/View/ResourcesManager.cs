using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View
{
    public class ResourcesManager : INotifyPropertyChanged, IDisposable
    {
        private static readonly Color ForegroundUIColor_Dark = Color.FromRgb(64, 64, 64);
        private static readonly Color ForegroundUIColor_Light = Color.FromRgb(243, 243, 243);
        private static readonly Color BackgroundUIColor_Dark = ForegroundUIColor_Light;
        private static readonly Color BackgroundUIColor_Light = ForegroundUIColor_Dark;

        private static readonly ImageSource dropDown_Light = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/DropDown_Light.png"));
        private static readonly ImageSource dropDown_Dark = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/DropDown_Dark.png"));

        private static readonly ImageSource edit_Light = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Edit_Light.png"));
        private static readonly ImageSource edit_Dark = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Edit_Dark.png"));

        private static readonly ImageSource show_Light = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Show_Light.png"));
        private static readonly ImageSource show_Dark = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Show_Dark.png"));

        private static readonly Dictionary<IWpfTextView, ResourcesManager> instances = new Dictionary<IWpfTextView, ResourcesManager>();

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

        public Color ForegroundUIColor => useDark ? ForegroundUIColor_Dark : ForegroundUIColor_Light;

        public Color BackgroundUIColor => useDark ? BackgroundUIColor_Dark : BackgroundUIColor_Light;

        public event PropertyChangedEventHandler PropertyChanged;

        private ResourcesManager(IWpfTextView textView)
        {
            vsSettings = VsSettings.GetOrCreate(textView);
            OnEditorBackgroundColorChange(vsSettings.GetCommentsBackground().Color);
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
    }
}
