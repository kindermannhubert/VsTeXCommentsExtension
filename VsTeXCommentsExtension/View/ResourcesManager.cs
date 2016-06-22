using Microsoft.VisualStudio.Text.Editor;
using System;
using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View
{
    public class ResourcesManager : INotifyPropertyChanged
    {
        public static ResourcesManager Instance { get; } = new ResourcesManager();

        private bool useDark = true;
        private ImageSource dropDown_Light, dropDown_Dark;
        private ImageSource edit_Light, edit_Dark;
        private ImageSource show_Light, show_Dark;

        private ResourcesManager()
        {
            dropDown_Light = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/DropDown_Light.png"));
            dropDown_Dark = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/DropDown_Dark.png"));

            edit_Light = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Edit_Light.png"));
            edit_Dark = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Edit_Dark.png"));

            show_Light = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Show_Light.png"));
            show_Dark = new BitmapImage(new Uri("pack://application:,,,/VsTeXCommentsExtension;component/Resources/Show_Dark.png"));

            VisualStudioSettings.Instance.CommentsColorChanged += Instance_CommentsColorChanged;
        }

        private void Instance_CommentsColorChanged(IWpfTextView textView, SolidColorBrush foreground, SolidColorBrush background)
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
                }
            }
        }

        public ImageSource DropDown => useDark ? dropDown_Dark : dropDown_Light;

        public ImageSource Edit => useDark ? edit_Dark : edit_Light;

        public ImageSource Show => useDark ? show_Dark : show_Light;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
