using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension
{
    /// <summary>
    /// Interaction logic for TexCommentAdornment.xaml
    /// </summary>
    internal partial class TeXCommentAdornment : UserControl, ITagAdornment, IDisposable
    {
        private static readonly string HtmlTemplate = LoadHtmlTemplate();

        private readonly List<Span> spansOfChangesFromEditing = new List<Span>();
        private readonly Action<Span> refreshTags;
        private readonly Color foreground;
        private readonly Color background;
        private readonly HtmlRenderer htmlRenderer;

        private TeXCommentTag tag;
        private bool changeMadeWhileInEditMode;

        private bool isInEditMode;
        public bool IsInEditMode
        {
            get { return isInEditMode; }
            set
            {
                Debug.WriteLine($"Adornment {DebugIndex}: IsInEditMode={value}");
                isInEditMode = value;
                if (isInEditMode) spansOfChangesFromEditing.Clear();
                changeMadeWhileInEditMode = false;
                SetUpControlsVisibility();

                if (spansOfChangesFromEditing.Count > 0)
                {
                    var resultSpan = spansOfChangesFromEditing[0];
                    for (int i = 1; i < spansOfChangesFromEditing.Count; i++)
                    {
                        var span = spansOfChangesFromEditing[i];
                        resultSpan = new Span(Math.Min(resultSpan.Start, span.Start), Math.Max(resultSpan.End, span.End));
                    }
                    refreshTags(resultSpan);
                }
                else refreshTags(tag.Span);
            }
        }

        public IntraTextAdornmentTaggerDisplayMode DisplayMode { get; private set; }

        private static int debugIndexer;
        public int DebugIndex { get; } = debugIndexer++;

        public LineSpan LineSpan { get; private set; }

        public TeXCommentAdornment(
            TeXCommentTag tag,
            Color foreground,
            Color background,
            LineSpan lineSpan,
            Action<Span> refreshTags,
            IntraTextAdornmentTaggerDisplayMode defaultDisplayMode)
        {
            this.tag = tag;
            this.refreshTags = refreshTags;
            this.foreground = foreground;
            this.background = background;
            this.htmlRenderer = new HtmlRenderer(background);
            htmlRenderer.WebBrowserImageReady += WebBrowserImageReady;

            LineSpan = lineSpan;
            DisplayMode = defaultDisplayMode;
            DataContext = this;

            InitializeComponent();

            isInEditMode = false;
            SetUpControlsVisibility();
            UpdateImageAsync();
        }

        public void Update(TeXCommentTag tag, LineSpan lineSpan)
        {
            bool changed = this.tag.Text != tag.Text;
            if (IsInEditMode)
            {
                changeMadeWhileInEditMode |= changed;
            }
            else if (changed || imageControl.Source == null)
            {
                this.tag = tag;
                LineSpan = lineSpan;
                UpdateImageAsync();
            }
        }

        public void HandleTextBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            if (!IsInEditMode || args.Changes.Count == 0) return;

            var start = args.Changes[0].NewPosition;
            var end = args.Changes[args.Changes.Count - 1].NewEnd;

            spansOfChangesFromEditing.Add(new Span(start, end - start));
        }

        private void UpdateImageAsync()
        {
            imageControl.Source = null;

            var htmlContent = HtmlTemplate
                    .Replace("$BackgroundColor", $"rgb({background.R},{background.G},{background.B})")
                    .Replace("$ForegroundColor", $"rgb({foreground.R},{foreground.G},{foreground.B})")
                    .Replace("$Source", tag.GetTextWithoutCommentMarks());

            htmlRenderer.LoadContent(htmlContent);
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            DisplayMode = IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText;
            IsInEditMode = true;
        }

        private void ButtonShow_Click(object sender, RoutedEventArgs e)
        {
            DisplayMode = IntraTextAdornmentTaggerDisplayMode.HideOriginalText;
            if (changeMadeWhileInEditMode)
            {
                imageControl.Source = null;
            }
            IsInEditMode = false;
        }

        private void WebBrowserImageReady(object sender, BitmapSource e)
        {
            imageControl.Source = e;
            if (e != null)
            {
                // '0.5*' because of rendering upscaling
                imageControl.Width = 0.8 * 0.5 * e.Width;
                imageControl.Height = 0.8 * 0.5 * e.Height;
            }
            SetUpControlsVisibility();
        }

        private void SetUpControlsVisibility()
        {
            if (imageControl.Source == null)
            {
                imageControl.Visibility = Visibility.Collapsed;
                progressBar.Visibility = !IsInEditMode ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                imageControl.Visibility = !IsInEditMode ? Visibility.Visible : Visibility.Collapsed;
                progressBar.Visibility = Visibility.Collapsed;
            }

            btnEdit.Visibility = !IsInEditMode ? Visibility.Visible : Visibility.Collapsed;
            btnShow.Visibility = IsInEditMode ? Visibility.Visible : Visibility.Collapsed;
        }

        public void Dispose()
        {
            htmlRenderer?.Dispose();
        }

        private static string LoadHtmlTemplate()
        {
            using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("VsTeXCommentsExtension.Resources.TeXCommentTemplate.html")))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
