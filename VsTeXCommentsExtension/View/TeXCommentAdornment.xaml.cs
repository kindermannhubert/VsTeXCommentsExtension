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
        private readonly Action<Span> refreshTags;
        private readonly Action<bool> setIsInEditModeForAllAdornmentsInDocument;
        private readonly IWpfTextView textView;
        private readonly PreviewAdorner previewAdorner;
        private readonly IRenderingManager renderingManager;
        private readonly List<EventHandler> displayModeChangedHandlers = new List<EventHandler>();

        private bool isInvalidated;
        private double lastLineWidthWithoutStartWhiteSpaces;

        public TeXCommentTag DataTag { get; private set; }
        public bool IsInEditMode => currentState == TeXCommentAdornmentState.EditingAndRenderingPreview || currentState == TeXCommentAdornmentState.EditingWithPreview;

        private TeXCommentAdornmentState currentState;
        public TeXCommentAdornmentState CurrentState
        {
            get { return currentState; }
            set
            {
                Debug.WriteLine($"Adornment {Index}: changing state from '{currentState}' to '{value}'");

                var lastDisplayMode = DisplayMode;
                currentState = value;
                if (lastDisplayMode != DisplayMode)
                {
                    foreach (var handler in displayModeChangedHandlers) handler(this, null);
                    refreshTags(DataTag.TeXBlock.Span);
                }

                OnPropertyChanged(nameof(CurrentState));

                switch (currentState)
                {
                    case TeXCommentAdornmentState.Rendering:
                    case TeXCommentAdornmentState.EditingAndRenderingPreview:
                        UpdateImageAsync();
                        break;
                    case TeXCommentAdornmentState.EditingWithPreview:
                    case TeXCommentAdornmentState.Rendered:
                    default:
                        break;
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
                    case TeXCommentAdornmentState.EditingAndRenderingPreview:
                    case TeXCommentAdornmentState.EditingWithPreview:
                        return IntraTextAdornmentTaggerDisplayMode.DoNotHideOriginalText_BeforeLastLineBreak;
                    case TeXCommentAdornmentState.Rendered:
                        return IntraTextAdornmentTaggerDisplayMode.HideOriginalText_WithoutLastLineBreak;
                    default:
                        throw new InvalidOperationException($"Unknown state: {currentState}.");
                }
            }
        }

        public event EventHandler DisplayModeChanged
        {
            add { displayModeChangedHandlers.Add(value); }
            remove { displayModeChangedHandlers.Remove(value); }
        }

        private static int debugIndexer;

        public event PropertyChangedEventHandler PropertyChanged;

        public int Index { get; } = debugIndexer++;

        public LineSpan LineSpan { get; private set; }

        public TeXCommentAdornment(
            IWpfTextView textView,
            TeXCommentTag tag,
            LineSpan lineSpan,
            double lastLineWidthWithoutStartWhiteSpaces,
            TeXCommentAdornmentState initialState,
            Action<Span> refreshTags,
            Action<bool> setIsInEditModeForAllAdornmentsInDocument,
            IRenderingManager renderingManager,
            IVsSettings vsSettings)
        {
            ExtensionSettings.Instance.CustomZoomChanged += CustomZoomChanged;
            textView.Caret.PositionChanged += Caret_PositionChanged;

            this.DataTag = tag;
            this.refreshTags = refreshTags;
            this.setIsInEditModeForAllAdornmentsInDocument = setIsInEditModeForAllAdornmentsInDocument;
            this.textView = textView;
            this.lastLineWidthWithoutStartWhiteSpaces = lastLineWidthWithoutStartWhiteSpaces;
            this.renderingManager = renderingManager;
            VsSettings = vsSettings;
            ResourcesManager = ResourcesManager.GetOrCreate(textView);
            LineSpan = lineSpan;

            InitializeComponent();

            previewAdorner = new PreviewAdorner(this, ResourcesManager, vsSettings);
            Loaded += (s, e) =>
            {
                if (previewAdorner.Parent == null)
                {
                    previewAdorner.OffsetX = -this.lastLineWidthWithoutStartWhiteSpaces; //'this' is important because of lambda closure
                    System.Windows.Documents.AdornerLayer.GetAdornerLayer(this).Add(previewAdorner);
                }
            };

            //for correctly working binding
            NameScope.SetNameScope(btnShow.ContextMenu, NameScope.GetNameScope(this));
            NameScope.SetNameScope(btnEdit.ContextMenu, NameScope.GetNameScope(this));
            NameScope.SetNameScope((ToolTip)imgError.ToolTip, NameScope.GetNameScope(this));

            CurrentState = initialState;
        }

        public void Update(TeXCommentTag tag, LineSpan lineSpan, double? lastLineWidthWithoutStartWhiteSpaces)
        {
            bool changed = this.DataTag.Text != tag.Text;

            this.DataTag = tag;
            LineSpan = lineSpan;
            if (lastLineWidthWithoutStartWhiteSpaces.HasValue)
            {
                this.lastLineWidthWithoutStartWhiteSpaces = lastLineWidthWithoutStartWhiteSpaces.Value;
                previewAdorner.OffsetX = -lastLineWidthWithoutStartWhiteSpaces.Value;
            }

            if (changed || isInvalidated)
            {
                switch (currentState)
                {
                    case TeXCommentAdornmentState.Rendered:
                    case TeXCommentAdornmentState.Rendering:
                        CurrentState = TeXCommentAdornmentState.Rendering;
                        break;
                    case TeXCommentAdornmentState.EditingWithPreview:
                    case TeXCommentAdornmentState.EditingAndRenderingPreview:
                        CurrentState = TeXCommentAdornmentState.EditingAndRenderingPreview;
                        break;
                    default:
                        break;
                }

                OnPropertyChanged(nameof(IsCaretInsideTeXBlock));
                isInvalidated = false;
            }
        }

        public void Invalidate()
        {
            isInvalidated = true;
            if (CurrentState == TeXCommentAdornmentState.Rendered) CurrentState = TeXCommentAdornmentState.Rendering;
            else if (CurrentState == TeXCommentAdornmentState.EditingWithPreview) CurrentState = TeXCommentAdornmentState.EditingAndRenderingPreview;
        }

        public void HandleTextBufferChanged(object sender, TextContentChangedEventArgs args)
        {
            if (!IsInEditMode || args.Changes.Count == 0) return;

            //when caret is moving while typing the PositionChanged event is not send
            OnPropertyChanged(nameof(IsCaretInsideTeXBlock));
        }

        public void Dispose()
        {
            ExtensionSettings.Instance.CustomZoomChanged -= CustomZoomChanged;
            textView.Caret.PositionChanged -= Caret_PositionChanged;
            ResourcesManager?.Dispose();
            displayModeChangedHandlers.Clear();
        }

        private void UpdateImageAsync()
        {
            var input = new HtmlRenderer.Input(
                DataTag,
                (0.01 * VsSettings.ZoomPercentage) * (0.01 * DataTag.TeXBlock.ZoomPercentage),
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

        private void Caret_PositionChanged(object sender, CaretPositionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsCaretInsideTeXBlock));
        }
    }
}
