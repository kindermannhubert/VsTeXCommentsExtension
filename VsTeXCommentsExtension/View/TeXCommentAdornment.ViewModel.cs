using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using VsTeXCommentsExtension.Integration;

namespace VsTeXCommentsExtension.View
{
    internal partial class TeXCommentAdornment
    {
        public ResourcesManager ResourcesManager { get; }

        public IVsSettings VsSettings { get; }

        private RendererResult? renderedResult;
        public RendererResult? RenderedResult
        {
            get { return renderedResult; }
            set
            {
                renderedResult = value;
                OnPropertyChanged(nameof(ErrorsSummary));
                OnPropertyChanged(nameof(AnyRenderingErrors));
                OnPropertyChanged(nameof(RenderedImage));
                OnPropertyChanged(nameof(RenderedImageWidth));
                OnPropertyChanged(nameof(RenderedImageHeight));
            }
        }

        public bool AnyRenderingErrors => renderedResult.HasValue && renderedResult.Value.HasErrors;
        public string ErrorsSummary => renderedResult?.ErrorsSummary ?? string.Empty;
        public BitmapSource RenderedImage => renderedResult?.Image;
        public double RenderedImageWidth => (renderedResult?.Image.Width / (textView.ZoomLevel * 0.01)) ?? 0;
        public double RenderedImageHeight => (renderedResult?.Image.Height / (textView.ZoomLevel * 0.01)) ?? 0;

        public bool IsCaretInsideTeXBlock
        {
            get
            {
                var spanWithLastLineBreak = tag.SpanWithLastLineBreak;
                Debug.Assert(spanWithLastLineBreak.Length >= tag.TeXBlock.FirstLineWhiteSpacesAtStart + TextSnapshotTeXCommentBlocks.TeXCommentPrefix.Length);
                spanWithLastLineBreak = spanWithLastLineBreak.TranslateStart(tag.TeXBlock.FirstLineWhiteSpacesAtStart + TextSnapshotTeXCommentBlocks.TeXCommentPrefix.Length);

                var caretPosition = textView.Caret.Position.BufferPosition;
                if (tag.Span.Length == spanWithLastLineBreak.Length)
                {
                    return caretPosition >= spanWithLastLineBreak.Start && caretPosition <= spanWithLastLineBreak.End;
                }
                return spanWithLastLineBreak.Contains(caretPosition);
            }
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
