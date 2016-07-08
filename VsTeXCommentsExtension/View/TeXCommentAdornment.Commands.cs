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

        public SnippetMenuItem[] Snippets_Fraction { get; } = new[]
        {
            new SnippetMenuItem("\\frac{a}{b}", "Snippets/Fraction/1.png"),
            new SnippetMenuItem("\\frac{dy}{dx}", "Snippets/Fraction/2.png"),
            new SnippetMenuItem("\\frac{\\Delta y}{\\Delta x}", "Snippets/Fraction/3.png"),
            new SnippetMenuItem("\\frac{\\partial y}{\\partial x}", "Snippets/Fraction/4.png"),
            new SnippetMenuItem("\\frac{\\delta y}{\\delta x}", "Snippets/Fraction/5.png"),
            new SnippetMenuItem("\\frac{\\pi}{2}", "Snippets/Fraction/6.png"),
        };

        public SnippetMenuItem[] Snippets_Script { get; } = new[]
        {
            new SnippetMenuItem("a^b", "Snippets/Script/1.png"),
            new SnippetMenuItem("a_b", "Snippets/Script/2.png"),
            new SnippetMenuItem("a_b^c", "Snippets/Script/3.png"),
            new SnippetMenuItem("{}_{a}^b{c}", "Snippets/Script/4.png"),
            new SnippetMenuItem("x_{y^2}", "Snippets/Script/5.png"),
            new SnippetMenuItem("e^{-i \\omega t}", "Snippets/Script/6.png"),
        };

        public SnippetMenuItem[] Snippets_Radical { get; } = new[]
        {
             new SnippetMenuItem("\\sqrt{a}", "Snippets/Radical/1.png"),
             new SnippetMenuItem("\\sqrt{n}{a}", "Snippets/Radical/2.png"),
             new SnippetMenuItem("\\sqrt{a^2+b^2}", "Snippets/Radical/3.png"),
        };

        public SnippetMenuItem[] Snippets_Integral { get; } = new[]
        {
            new SnippetMenuItem("\\int{f(x)}\\,\\mathrm{d}x", "Snippets/Integral/1.png"),
            new SnippetMenuItem("\\int_{a}^{b}{f(x)}\\,\\mathrm{d}x", "Snippets/Integral/2.png"),
            new SnippetMenuItem("\\iint_V{f(u,v)}\\,\\mathrm{d}u\\,\\mathrm{d}v", "Snippets/Integral/3.png"),
            new SnippetMenuItem("\\iiint_V{f(u,v,w)}\\,\\mathrm{d}u\\,\\mathrm{d}v\\,\\mathrm{d}w", "Snippets/Integral/4.png"),
            new SnippetMenuItem("\\idotsint_V{f(u_1,\\dots,u_k)}\\,\\mathrm{d}u_1\\dots\\,\\mathrm{d}u_k", "Snippets/Integral/5.png"),
            new SnippetMenuItem("\\oint_V{f(s)}\\,\\mathrm{d}s", "Snippets/Integral/6.png"),
        };

        public SnippetMenuItem[] Snippets_LargeOperator { get; } = new[]
        {
            new SnippetMenuItem("\\sum_{i=1}^{N}{n_i}", "Snippets/LargeOperator/1.png"),
            new SnippetMenuItem("\\prod_{i=1}^{N}{n_i}", "Snippets/LargeOperator/2.png"),
            new SnippetMenuItem("\\bigcup_{i=1}^{N}{X_i}", "Snippets/LargeOperator/3.png"),
            new SnippetMenuItem("\\bigcap_{i=1}^{N}{X_i}", "Snippets/LargeOperator/4.png"),
            new SnippetMenuItem("\\bigwedge_{i=1}^{N}{n_i}", "Snippets/LargeOperator/5.png"),
            new SnippetMenuItem("\\bigvee_{i=1}^{N}{n_i}", "Snippets/LargeOperator/6.png"),
        };

        public SnippetMenuItem[] Snippets_Matrix { get; } = new[]
        {
            new SnippetMenuItem(
@"
//\begin{matrix}
//  a & b \\
//  c & d \\
//\end{matrix}
//",
"Snippets/Matrix/1.png"),
                        new SnippetMenuItem(
@"
//\begin{pmatrix}
//  a & b \\
//  c & d \\
//\end{pmatrix}
//",
"Snippets/Matrix/2.png"),
                        new SnippetMenuItem(
@"
//\begin{bmatrix}
//  a & b \\
//  c & d \\
//\end{bmatrix}
//",
"Snippets/Matrix/3.png"),
                        new SnippetMenuItem(
@"
//\begin{Bmatrix}
//  a & b \\
//  c & d \\
//\end{Bmatrix}
//",
"Snippets/Matrix/4.png"),
                        new SnippetMenuItem(
@"
//\begin{vmatrix}
//  a & b \\
//  c & d \\
//\end{vmatrix}
//",
"Snippets/Matrix/5.png"),
                        new SnippetMenuItem(
@"
//\begin{Vmatrix}
//  a & b \\
//  c & d \\
//\end{Vmatrix}
//",
"Snippets/Matrix/6.png"),
                        new SnippetMenuItem(
@"
//\begin{pmatrix}
//  a & b & c \\
//  d & e & f \\
//  g & h & i \\
//\end{pmatrix}
//",
"Snippets/Matrix/7.png"),
                        new SnippetMenuItem(
@"
//\begin{pmatrix}
//  a_{1,1} & \cdots & a_{1,n} \\
//  \vdots & \ddots & \vdots \\
//  a_{n,1} & \cdots & a_{n,n} \\
//\end{pmatrix}
//",
"Snippets/Matrix/8.png"),
                        new SnippetMenuItem(
@"
//\left(
//    \begin{array}{cc|c}
//      a & b & c \\
//      d & e & f
//    \end{array}
//\right)
//",
"Snippets/Matrix/9.png"),
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
            public string Snippet { get; }
            public ImageSource Icon { get; }

            public SnippetMenuItem(string snippet, string iconPath)
            {
                Snippet = snippet;
                Icon = new BitmapImage(ResourcesManager.GetAssemblyResourceUri(iconPath));
            }
        }
    }
}
