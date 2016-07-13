using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View
{
    internal partial class TeXCommentAdornment
    {
        public static ZoomMenuItem[] ZoomMenuItems { get; } = new[]
        {
            new ZoomMenuItem(0.5), new ZoomMenuItem(0.6), new ZoomMenuItem(0.7), new ZoomMenuItem(0.8), new ZoomMenuItem(0.9), new ZoomMenuItem(1, true),
            new ZoomMenuItem(1.1), new ZoomMenuItem(1.2), new ZoomMenuItem(1.3), new ZoomMenuItem(1.4), new ZoomMenuItem(1.5), new ZoomMenuItem(1.6),
            new ZoomMenuItem(1.7), new ZoomMenuItem(1.8), new ZoomMenuItem(1.9), new ZoomMenuItem(2)
        };

        public static SnippetMenuItem[] Snippets_Fractions { get; } = LoadSnippets("Fractions");
        public static SnippetMenuItem[] Snippets_Scripts { get; } = LoadSnippets("Scripts");
        public static SnippetMenuItem[] Snippets_Radicals { get; } = LoadSnippets("Radicals");
        public static SnippetMenuItem[] Snippets_Integrals { get; } = LoadSnippets("Integrals");
        public static SnippetMenuItem[] Snippets_LargeOperators { get; } = LoadSnippets("LargeOperators");
        public static SnippetMenuItem[] Snippets_Matrices { get; } = LoadSnippets("Matrices");
        public static SnippetMenuItem[] Snippets_GreekLowers { get; } = LoadSnippets("GreekLowers");
        public static SnippetMenuItem[] Snippets_GreekUppers { get; } = LoadSnippets("GreekUppers");
        public static SnippetMenuItem[] Snippets_BinaryOperations { get; } = LoadSnippets("BinaryOperations");
        public static SnippetMenuItem[] Snippets_Relations { get; } = LoadSnippets("Relations");
        public static SnippetMenuItem[] Snippets_Arrows { get; } = LoadSnippets("Arrows");
        public static SnippetMenuItem[] Snippets_Miscellaneous { get; } = LoadSnippets("Miscellaneous");
        public static SnippetMenuItem[] Snippets_Functions { get; } = LoadSnippets("Functions");
        public static SnippetMenuItem[] Snippets_Delimiters { get; } = LoadSnippets("Delimiters");

        private static SnippetMenuItem[] LoadSnippets(string group)
        {
            using (var stream = Application.GetResourceStream(ResourcesManager.GetAssemblyResourceUri("Snippets/Snippets.xml")).Stream)
            {
                var cfgElement = XElement.Load(stream);
                return cfgElement.Elements("Snippet")
                    .Where(e => e.Element("Group").Value == group)
                    .Select(e => new SnippetMenuItem(e.Element("Code").Value.Replace("\n", "\r\n"), e.Element("Icon").Value))
                    .ToArray();
            }
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            CurrentState = TeXCommentAdornmentState.EditingAndRenderingPreview;
        }

        private void ButtonShow_Click(object sender, RoutedEventArgs e)
        {
            CurrentState = TeXCommentAdornmentState.Rendering;
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
            if (!renderedResult.HasValue) return;

            try
            {
                var result = renderedResult.Value;
                var path = result.CachePath;
                if (path == null) return;
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
            public string Snippet { get; }
            public ImageSource Icon { get; }

            public SnippetMenuItem(string snippet, string iconPath)
            {
                Snippet = snippet;

                var iconUri = ResourcesManager.GetAssemblyResourceUri(iconPath);

                using (var stream = Application.GetResourceStream(iconUri).Stream)
                using (var bmp = new System.Drawing.Bitmap(stream))
                {
                    Icon = ResourcesManager.CreateBitmapSourceWithCurrentDpi(bmp);
                }
            }
        }
    }
}
