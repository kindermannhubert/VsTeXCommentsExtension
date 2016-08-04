using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using VsTeXCommentsExtension.Integration;
using VsTeXCommentsExtension.SyntaxHighlighting;

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
                OnPropertyChanged(nameof(AnyErrors));
                OnPropertyChanged(nameof(RenderedImage));
                OnPropertyChanged(nameof(RenderedImageWidth));
                OnPropertyChanged(nameof(RenderedImageHeight));
            }
        }

        public bool AnyErrors => DataTag.SyntaxErrors != null || (renderedResult.HasValue && renderedResult.Value.HasErrors);
        public string ErrorsSummary
        {
            get
            {
                if (DataTag.SyntaxErrors != null) return DataTag.SyntaxErrors;
                else return renderedResult?.ErrorsSummary ?? string.Empty;
            }
        }
        public BitmapSource RenderedImage => renderedResult?.Image;
        public double RenderedImageWidth => (renderedResult?.Image.Width / (textView.ZoomLevel * 0.01)) ?? 0;
        public double RenderedImageHeight => (renderedResult?.Image.Height / (textView.ZoomLevel * 0.01)) ?? 0;

        public bool IsCaretInsideTeXBlock
        {
            get
            {
                var spanWithLastLineBreak = DataTag.SpanWithLastLineBreak;
                Debug.Assert(spanWithLastLineBreak.Length >= DataTag.TeXBlock.FirstLineWhiteSpacesAtStart + TextSnapshotTeXCommentBlocks.TeXCommentPrefix.Length);
                spanWithLastLineBreak = spanWithLastLineBreak.TranslateStart(DataTag.TeXBlock.FirstLineWhiteSpacesAtStart + TextSnapshotTeXCommentBlocks.TeXCommentPrefix.Length);

                var caretPosition = textView.Caret.Position.BufferPosition;
                if (DataTag.Span.Length == DataTag.SpanWithLastLineBreak.Length)
                {
                    return caretPosition >= spanWithLastLineBreak.Start && caretPosition <= spanWithLastLineBreak.End;
                }
                return spanWithLastLineBreak.Contains(caretPosition);
            }
        }

        public bool IsCaretInsideMathBlock
        {
            get
            {
                if (!IsCaretInsideTeXBlock) return false;

                var spanWithLastLineBreak = DataTag.SpanWithLastLineBreak;
                Debug.Assert(spanWithLastLineBreak.Length >= DataTag.TeXBlock.FirstLineWhiteSpacesAtStart);
                var textStartIndex = spanWithLastLineBreak.Start + DataTag.TeXBlock.FirstLineWhiteSpacesAtStart;

                var caretPositionRelativeToTextStart = textView.Caret.Position.BufferPosition.Position - textStartIndex;
                foreach (Match mathBlockMatch in TeXSyntaxClassifier.MathBlockRegex.Matches(DataTag.Text))
                {
                    if (mathBlockMatch.Success)
                    {
                        var dollarSignCount = mathBlockMatch.Groups[1].Length;
                        if (caretPositionRelativeToTextStart >= mathBlockMatch.Index + dollarSignCount &&
                            caretPositionRelativeToTextStart <= mathBlockMatch.Index + mathBlockMatch.Length - dollarSignCount)
                            return true;
                    }
                }

                //special case
                if (caretPositionRelativeToTextStart > 0 && caretPositionRelativeToTextStart < DataTag.Text.Length)
                {
                    if (DataTag.Text[caretPositionRelativeToTextStart - 1] == '$' &&
                        DataTag.Text[caretPositionRelativeToTextStart] == '$')
                        return true;
                }

                return false;
            }
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
