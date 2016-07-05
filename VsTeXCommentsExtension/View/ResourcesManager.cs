using Microsoft.VisualStudio.Text.Editor;
using System;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View
{
    public class ResourcesManager : INotifyPropertyChanged
    {
        private static readonly Color ForegroundUIColor_Dark = Color.FromRgb(64, 64, 64);
        private static readonly Color ForegroundUIColor_Light = Color.FromRgb(243, 243, 243);
        private static readonly Color BackgroundUIColor_Dark = ForegroundUIColor_Light;
        private static readonly Color BackgroundUIColor_Light = ForegroundUIColor_Dark;

        public static ResourcesManager Instance { get; } = new ResourcesManager();

        private readonly ImageSource dropDown_Light, dropDown_Dark;
        private readonly ImageSource edit_Light, edit_Dark;
        private readonly ImageSource show_Light, show_Dark;

        private bool useDark = true;

        private ResourcesManager()
        {
            dropDown_Light = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/DropDown_Light.png"));
            dropDown_Dark = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/DropDown_Dark.png"));

            edit_Light = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Edit_Light.png"));
            edit_Dark = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Edit_Dark.png"));

            show_Light = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Show_Light.png"));
            show_Dark = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Show_Dark.png"));

            VisualStudioSettings.Instance.CommentsColorChanged += CommentsColorChanged;
        }

        private void CommentsColorChanged(IWpfTextView textView, SolidColorBrush foreground, SolidColorBrush background)
        {
            ChangeEditorBackgroundColor(background.Color);
        }

        private Color editorBackgroundColor;
        private void ChangeEditorBackgroundColor(Color color)
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

        public ImageSource DropDown => useDark ? dropDown_Dark : dropDown_Light;

        public ImageSource Edit => useDark ? edit_Dark : edit_Light;

        public ImageSource Show => useDark ? show_Dark : show_Light;

        public Color ForegroundUIColor => useDark ? ForegroundUIColor_Dark : ForegroundUIColor_Light;

        public Color BackgroundUIColor => useDark ? BackgroundUIColor_Dark : BackgroundUIColor_Light;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
