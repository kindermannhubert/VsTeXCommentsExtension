using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View
{
    internal partial class TeXCommentAdornment
    {
        public ZoomMenuItem[] ZoomMenuItems { get; } = new[]
        {
            new ZoomMenuItem(0.5), new ZoomMenuItem(0.6), new ZoomMenuItem(0.7), new ZoomMenuItem(0.8), new ZoomMenuItem(0.9), new ZoomMenuItem(1, true),
            new ZoomMenuItem(1.1), new ZoomMenuItem(1.2), new ZoomMenuItem(1.3), new ZoomMenuItem(1.4), new ZoomMenuItem(1.5), new ZoomMenuItem(1.6),
            new ZoomMenuItem(1.7), new ZoomMenuItem(1.8), new ZoomMenuItem(1.9), new ZoomMenuItem(2)
        };

        public SnippetMenuItem[] Snippets { get; } = new[]
        {
            new SnippetMenuItem("Fraction", "\\frac{a}{b}", "Snippets/Fraction.png"),
            new SnippetMenuItem("Square root", "\\sqrt{x}", "Snippets/SquareRoot.png")
        };

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            CurrentState = TeXCommentAdornmentState.Editing;
        }

        private void ButtonShow_Click(object sender, RoutedEventArgs e)
        {
            CurrentState = TeXCommentAdornmentState.Shown;
        }

        private void CustomZoomChanged(double zoomScale)
        {
            foreach (var item in ZoomMenuItems)
            {
                item.IsChecked = item.ZoomScale == zoomScale;
            }
        }

        private void MenuItem_EditAll_Click(object sender, RoutedEventArgs e)
        {
            setIsInEditModeForAllAdornmentsInDocument(true);
        }

        private void MenuItem_ShowAll_Click(object sender, RoutedEventArgs e)
        {
            setIsInEditModeForAllAdornmentsInDocument(false);
        }

        private void MenuItem_OpenImageCache_Click(object sender, RoutedEventArgs e)
        {
            if (imageControl.Source == null || imageControl.Tag == null) return;

            try
            {
                var path = (string)imageControl.Tag;
                var processArgs = $"/e, /select,\"{path}\"";
                Process.Start(new ProcessStartInfo("explorer", processArgs));
            }
            catch { }
        }

        private void MenuItem_ChangeZoom_Click(object sender, RoutedEventArgs e)
        {
            var item = (MenuItem)sender;
            var itemHeader = item.Header.ToString();
            var customZoomScale = 0.01 * int.Parse(itemHeader.Substring(0, itemHeader.Length - 1));

            ExtensionSettings.Instance.CustomZoomScale = customZoomScale; //will trigger zoom changed event
        }

        private void MenuItem_InsertSnippet_Click(object sender, RoutedEventArgs e)
        {
            var snippet = (sender as MenuItem)?.DataContext as SnippetMenuItem;
            if (snippet == null) return;

            var caret = textView.Caret;
            caret.EnsureVisible();
            textView.TextBuffer.Insert(caret.Position.BufferPosition.Position, snippet.Snippet);
        }

        public class ZoomMenuItem : INotifyPropertyChanged
        {
            public double ZoomScale { get; }

            private bool isChecked;
            public bool IsChecked
            {
                get { return isChecked; }
                set
                {
                    isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public ZoomMenuItem(double zoomScale, bool isChecked = false)
            {
                ZoomScale = zoomScale;
                this.isChecked = isChecked;
            }

            public override string ToString() => $"{100 * ZoomScale}%";
        }

        public class SnippetMenuItem
        {
            public string Header { get; }
            public string Snippet { get; }
            public ImageSource Icon { get; }

            public SnippetMenuItem(string header, string snippet, string iconPath)
            {
                Header = header;
                Snippet = snippet;
                Icon = new BitmapImage(ResourcesManager.GetAssemblyResourceUri(iconPath));
            }
        }
    }
}
