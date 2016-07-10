using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View
{
    /// <summary>
    /// Interaction logic for TexCommentAdornment.xaml
    /// </summary>
    internal partial class TeXCommentAdornment : UserControl, ITagAdornment, IDisposable, INotifyPropertyChanged
    {
        private readonly List<Span> spansOfChangesFromEditing = new List<Span>();
        private readonly Action<Span> refreshTags;
        private readonly Action<bool> setIsInEditModeForAllAdornmentsInDocument;
        private readonly IWpfTextView textView;
        private readonly IRenderingManager renderingManager;

        private TeXCommentTag tag;
        private bool changeMadeWhileInEditMode;
        private bool isInvalidated;

        public bool IsInEditMode => currentState == TeXCommentAdornmentState.EditingAndRenderingPreview || currentState == TeXCommentAdornmentState.EditingWithPreview;

        private TeXCommentAdornmentState currentState;
        public TeXCommentAdornmentState CurrentState
        {
            get { return currentState; }
            set
            {
                switch (value)
                {
                    case TeXCommentAdornmentState.Rendered:
                    case TeXCommentAdornmentState.EditingWithPreview:
                        break;
                    case TeXCommentAdornmentState.Rendering:
                    case TeXCommentAdornmentState.EditingAndRenderingPreview:
                        throw new InvalidOperationException($"Setting invalid state '{value}'.");
                }

                if (changeMadeWhileInEditMode) RenderedResult = null;
                if (value == TeXCommentAdornmentState.Rendered && !renderedResult.HasValue) value = TeXCommentAdornmentState.Rendering;
                if (value == TeXCommentAdornmentState.EditingWithPreview && !renderedResult.HasValue) value = TeXCommentAdornmentState.EditingAndRenderingPreview;

                Debug.WriteLine($"Adornment {Index}: changing state from '{currentState}' to '{value}'");

                currentState = value;
                if (IsInEditMode) spansOfChangesFromEditing.Clear();
                changeMadeWhileInEditMode = false;

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

                OnPropertyChanged(nameof(CurrentState));
            }
        }

        public IntraTextAdornmentTaggerDisplayMode DisplayMode
        {
            get
            {
                switch (currentState)
                {
                    case TeXCommentAdornmentState.Rendering:
                    case TeXCommentAdornmentState.EditingAndRenderingPreview:
                        return IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText_BeforeLastLineBreak;
                    case TeXCommentAdornmentState.EditingWithPreview:
                        return IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText_AfterLastLineBreak;
                    case TeXCommentAdornmentState.Rendered:
                        return IntraTextAdornmentTaggerDisplayMode.HideOriginalText_WithoutLastLineBreak;
                    default:
                        throw new InvalidOperationException($"Unknown state: {currentState}.");
                }
            }
        }

        private static int debugIndexer;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Index { get; } = debugIndexer++;

        public LineSpan LineSpan { get; private set; }

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
            VsSettings = vsSettings;
            ResourcesManager = ResourcesManager.GetOrCreate(textView);
            LineSpan = lineSpan;

            InitializeComponent();

            //for correctly working binding
            NameScope.SetNameScope(btnShow.ContextMenu, NameScope.GetNameScope(this));
            NameScope.SetNameScope(btnEdit.ContextMenu, NameScope.GetNameScope(this));
            NameScope.SetNameScope((ToolTip)imgError.ToolTip, NameScope.GetNameScope(this));

            CurrentState = TeXCommentAdornmentState.Rendered;
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

            if (changed || isInvalidated)
            {
                this.tag = tag;
                isInvalidated = false;
                UpdateImageAsync();
            }
        }

        public void Invalidate()
        {
            isInvalidated = true;
            RenderedResult = null;
            if (CurrentState == TeXCommentAdornmentState.Rendered) CurrentState = TeXCommentAdornmentState.Rendered;
            else if (CurrentState == TeXCommentAdornmentState.EditingWithPreview) CurrentState = TeXCommentAdornmentState.EditingWithPreview;
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
            ResourcesManager?.Dispose();
        }

        private void UpdateImageAsync()
        {
            RenderedResult = null;

            var input = new HtmlRenderer.Input(
                tag.GetTextWithoutCommentMarks(),
                0.01 * VsSettings.ZoomPercentage,
                VsSettings.CommentsForeground.Color,
                VsSettings.CommentsBackground.Color,
                VsSettings.CommentsFont,
                textView,
                this);
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
                RenderedResult = result;
                if (CurrentState == TeXCommentAdornmentState.Rendering) CurrentState = TeXCommentAdornmentState.Rendered;
                else if (CurrentState == TeXCommentAdornmentState.EditingAndRenderingPreview) CurrentState = TeXCommentAdornmentState.EditingWithPreview;
            }
        }
    }
}
