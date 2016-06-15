using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension
{
    /// <summary>
    /// Interaction logic for TexCommentAdornment.xaml
    /// </summary>
    internal partial class TeXCommentAdornment : UserControl, ITagAdornment
    {
        private readonly List<Span> spansOfChangesFromEditing = new List<Span>();
        private readonly Action<Span> refreshTags;
        private readonly Color background;

        private TeXCommentTag tag;
        private bool changeMadeWhileInEditMode;

        private bool isInEditMode;
        private bool IsInEditMode
        {
            get { return isInEditMode; }
            set
            {
                isInEditMode = value;
                if (isInEditMode) spansOfChangesFromEditing.Clear();
                changeMadeWhileInEditMode = false;
                SetUpControlsVisibility();
            }
        }

        public IntraTextAdornmentTaggerDisplayMode DisplayMode { get; private set; }

        private static int debugIndexer;
        public int DebugIndex { get; } = debugIndexer++;

        public TeXCommentAdornment(TeXCommentTag tag, Color background, Action<Span> refreshTags, IntraTextAdornmentTaggerDisplayMode defaultDisplayMode)
        {
            this.tag = tag;
            this.refreshTags = refreshTags;
            this.background = background;

            DisplayMode = defaultDisplayMode;
            DataContext = this;

            InitializeComponent();

            IsInEditMode = false;
            UpdateImageAsync();
        }

        public void Update(TeXCommentTag tag)
        {
            bool changed = this.tag.Text != tag.Text;
            if (IsInEditMode)
            {
                changeMadeWhileInEditMode |= changed;
            }
            else if (changed || imageControl.Source == null)
            {
                this.tag = tag;
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

            var webBrowser = new WebBrowserUtility(background);
            webBrowser.WebBrowserImageReady += WebBrowserImageReady;

            var fileContent = File.ReadAllText("Z:\\mathjaxtest.html");
            File.WriteAllText(
                "Z:\\temp.html",
                fileContent
                    .Replace("$Color", $"rgb({background.R},{background.G},{background.B})")
                    .Replace("$Source", tag.GetTextWithoutCommentMarks()));

            webBrowser.Navigate(new Uri("Z:\\temp.html"));
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            DisplayMode = IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText;
            IsInEditMode = true;

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

        private void ButtonShow_Click(object sender, RoutedEventArgs e)
        {
            DisplayMode = IntraTextAdornmentTaggerDisplayMode.HideOriginalText;
            if (changeMadeWhileInEditMode)
            {
                imageControl.Source = null;
            }
            IsInEditMode = false;

            if (spansOfChangesFromEditing.Count > 0)
            {
                var resultSpan = spansOfChangesFromEditing[0];
                for (int i = 1; i < spansOfChangesFromEditing.Count; i++)
                {
                    var span = spansOfChangesFromEditing[i];
                    var newStart = Math.Min(resultSpan.Start, span.Start);
                    var newEnd = Math.Max(resultSpan.End, span.End);
                    resultSpan = new Span(newStart, newEnd - newStart);
                }
                refreshTags(resultSpan);
            }
            else refreshTags(tag.Span);
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
    }
}
