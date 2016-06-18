using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View
{
    /// <summary>
    /// Interaction logic for TexCommentAdornment.xaml
    /// </summary>
    internal partial class TeXCommentAdornment : UserControl, ITagAdornment
    {
        private const double RenderScale = 3;

        private readonly List<Span> spansOfChangesFromEditing = new List<Span>();
        private readonly Action<Span> refreshTags;
        private readonly Color foreground;
        private readonly Color background;
        private readonly System.Drawing.Font font;
        private readonly IRenderingManager renderingManager;

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
                DisplayMode = isInEditMode ? IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText : IntraTextAdornmentTaggerDisplayMode.HideOriginalText;
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
            System.Drawing.Font font,
            IRenderingManager renderingManager,
            LineSpan lineSpan,
            Action<Span> refreshTags,
            IntraTextAdornmentTaggerDisplayMode defaultDisplayMode)
        {
            this.tag = tag;
            this.refreshTags = refreshTags;
            this.foreground = foreground;
            this.background = background;
            this.font = font;
            this.renderingManager = renderingManager;

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
            else if (changed)
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

            var template = new TeXCommentHtmlTemplate()
            {
                BackgroundColor = background,
                ForegroundColor = foreground,
                FontFamily = font.FontFamily.Name,
                FontSize = RenderScale * font.Size,
                Source = tag.GetTextWithoutCommentMarks()
            };

            var htmlContent = template.TransformText();
            renderingManager.LoadContentAsync(htmlContent, ImageIsReady);
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            IsInEditMode = true;
        }

        private void ButtonShow_Click(object sender, RoutedEventArgs e)
        {
            if (changeMadeWhileInEditMode)
            {
                imageControl.Source = null;
            }
            IsInEditMode = false;
        }

        private void ImageIsReady(BitmapSource e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action<BitmapSource>(ImageIsReady), e);
            }
            else
            {
                imageControl.Source = e;
                if (e != null)
                {
                    imageControl.Width = e.Width / RenderScale;
                    imageControl.Height = e.Height / RenderScale;
                }
                SetUpControlsVisibility();
            }
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
    }
}
