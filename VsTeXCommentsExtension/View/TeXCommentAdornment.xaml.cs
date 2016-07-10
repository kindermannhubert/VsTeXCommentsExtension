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

                if (changeMadeWhileInEditMode) RenderedResult = null;
                if (value == TeXCommentAdornmentState.Shown && !renderedResult.HasValue) value = TeXCommentAdornmentState.Rendering;

                Debug.WriteLine($"Adornment {DebugIndex}: changing state from '{currentState}' to '{value}'");

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

        public event PropertyChangedEventHandler PropertyChanged;

        public int DebugIndex { get; } = debugIndexer++;

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
            RenderedResult = null;
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
                RenderedResult = result;
                if (CurrentState == TeXCommentAdornmentState.Rendering) CurrentState = TeXCommentAdornmentState.Shown;
            }
        }
    }
}
