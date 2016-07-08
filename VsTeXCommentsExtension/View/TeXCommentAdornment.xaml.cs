using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View
{
    /// <summary>
    /// Interaction logic for TexCommentAdornment.xaml
    /// </summary>
    internal partial class TeXCommentAdornment : UserControl, ITagAdornment, IDisposable
    {
        private readonly List<Span> spansOfChangesFromEditing = new List<Span>();
        private readonly Action<Span> refreshTags;
        private readonly Action<bool> setIsInEditModeForAllAdornmentsInDocument;
        private readonly IWpfTextView textView;
        private readonly IRenderingManager renderingManager;
        private readonly ResourcesManager resourcesManager;
        private readonly VsSettings vsSettings;

        private TeXCommentTag tag;
        private bool changeMadeWhileInEditMode;
        private bool isInvalidated;

        private bool IsInEditMode => currentState == TeXCommentAdornmentState.Editing;

        private TeXCommentAdornmentState currentState;
        public TeXCommentAdornmentState CurrentState
        {
            get { return currentState; }
            set
            {
                switch (value)
                {
                    case TeXCommentAdornmentState.Shown:
                    case TeXCommentAdornmentState.Editing:
                        break;
                    case TeXCommentAdornmentState.Rendering:
                        throw new InvalidOperationException($"Setting invalid state '{value}'.");
                }

                if (changeMadeWhileInEditMode) imageControl.Source = null;
                if (value == TeXCommentAdornmentState.Shown && imageControl.Source == null) value = TeXCommentAdornmentState.Rendering;

                Debug.WriteLine($"Adornment {DebugIndex}: changing state from '{currentState}' to '{value}'");

                currentState = value;
                if (IsInEditMode) spansOfChangesFromEditing.Clear();
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
                else
                {
                    refreshTags(tag.TeXBlock.Span);
                }
            }
        }

        public IntraTextAdornmentTaggerDisplayMode DisplayMode
        {
            get
            {
                switch (currentState)
                {
                    case TeXCommentAdornmentState.Rendering:
                    case TeXCommentAdornmentState.Editing:
                        return IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText;
                    case TeXCommentAdornmentState.Shown:
                        return IntraTextAdornmentTaggerDisplayMode.HideOriginalText;
                    default:
                        throw new InvalidOperationException($"Unknown state: {currentState}.");
                }
            }
        }

        private static int debugIndexer;
        public int DebugIndex { get; } = debugIndexer++;

        public LineSpan LineSpan { get; private set; }

        public ResourcesManager ResourcesManager => resourcesManager;

        public TeXCommentAdornment(
            IWpfTextView textView,
            TeXCommentTag tag,
            LineSpan lineSpan,
            Action<Span> refreshTags,
            Action<bool> setIsInEditModeForAllAdornmentsInDocument,
            IRenderingManager renderingManager,
            VsSettings vsSettings)
        {
            ExtensionSettings.Instance.CustomZoomChanged += CustomZoomChanged;

            this.tag = tag;
            this.refreshTags = refreshTags;
            this.setIsInEditModeForAllAdornmentsInDocument = setIsInEditModeForAllAdornmentsInDocument;
            this.textView = textView;
            this.renderingManager = renderingManager;
            this.vsSettings = vsSettings;
            this.resourcesManager = View.ResourcesManager.GetOrCreate(textView);

            LineSpan = lineSpan;
            DataContext = this;

            InitializeComponent();

            ReloadColors();
            vsSettings.CommentsColorChanged += VsSettings_CommentsColorChanged;

            CurrentState = TeXCommentAdornmentState.Shown;
            UpdateImageAsync();
        }

        public void Update(TeXCommentTag tag, LineSpan lineSpan)
        {
            LineSpan = lineSpan;

            bool changed = this.tag.Text != tag.Text;
            if (IsInEditMode)
            {
                changeMadeWhileInEditMode = changed;
            }
            else if (changed || isInvalidated)
            {
                this.tag = tag;
                isInvalidated = false;
                UpdateImageAsync();
            }
        }

        public void Invalidate()
        {
            isInvalidated = true;
            imageControl.Source = null;
            if (CurrentState != TeXCommentAdornmentState.Editing)
            {
                CurrentState = TeXCommentAdornmentState.Shown;
            }
        }

        public void HandleTextBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            if (!IsInEditMode || args.Changes.Count == 0) return;

            var start = args.Changes[0].NewPosition;
            var end = args.Changes[args.Changes.Count - 1].NewEnd;

            spansOfChangesFromEditing.Add(new Span(start, end - start));
        }

        public void Dispose()
        {
            ExtensionSettings.Instance.CustomZoomChanged -= CustomZoomChanged;
            vsSettings.CommentsColorChanged -= VsSettings_CommentsColorChanged;
            resourcesManager?.Dispose();
        }

        private void UpdateImageAsync()
        {
            imageControl.Source = null;

            var input = new HtmlRenderer.Input(
                tag.GetTextWithoutCommentMarks(),
                0.01 * vsSettings.ZoomPercentage,
                vsSettings.CommentsForeground.Color,
                vsSettings.CommentsBackground.Color,
                vsSettings.CommentsFont,
                textView);
            renderingManager.RenderAsync(input, ImageIsReady);
        }

        private void ImageIsReady(RendererResult result)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action<RendererResult>(ImageIsReady), result);
            }
            else
            {
                var img = result.Image;
                imageControl.Source = img;
                imageControl.Width = img.Width / (textView.ZoomLevel * 0.01);
                imageControl.Height = img.Height / (textView.ZoomLevel * 0.01);
                imageControl.Tag = result.CachePath;

                if (CurrentState == TeXCommentAdornmentState.Rendering) CurrentState = TeXCommentAdornmentState.Shown;
            }
        }

        private void SetUpControlsVisibility()
        {
            switch (currentState)
            {
                case TeXCommentAdornmentState.Rendering:
                    imageControl.Visibility = Visibility.Collapsed;
                    progressBar.Visibility = Visibility.Visible;
                    btnEdit.Visibility = Visibility.Visible;
                    btnShow.Visibility = Visibility.Collapsed;
                    leftBorderGroupPanel.Visibility = Visibility.Collapsed;
                    break;
                case TeXCommentAdornmentState.Shown:
                    imageControl.Visibility = Visibility.Visible;
                    progressBar.Visibility = Visibility.Collapsed;
                    btnEdit.Visibility = Visibility.Visible;
                    btnShow.Visibility = Visibility.Collapsed;
                    leftBorderGroupPanel.Visibility = Visibility.Visible;
                    break;
                case TeXCommentAdornmentState.Editing:
                    imageControl.Visibility = Visibility.Collapsed;
                    progressBar.Visibility = Visibility.Collapsed;
                    btnEdit.Visibility = Visibility.Collapsed;
                    btnShow.Visibility = Visibility.Visible;
                    leftBorderGroupPanel.Visibility = Visibility.Collapsed;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown state '{currentState}'.");
            }
        }

        private void ReloadColors()
        {
            leftBorderPanel1.Background = vsSettings.CommentsForeground;
            leftBorderPanel2.Background = vsSettings.CommentsForeground;
        }

        private void VsSettings_CommentsColorChanged(IWpfTextView textView, SolidColorBrush foreground, SolidColorBrush background)
        {
            ReloadColors();
        }
    }
}
