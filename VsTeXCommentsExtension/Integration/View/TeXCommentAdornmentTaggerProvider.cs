using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Windows.Media;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.View;

namespace VsTeXCommentsExtension.Integration.View
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [ContentType("projection")]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class TeXCommentAdornmentTaggerProvider : IViewTaggerProvider, IDisposable
    {
        private static readonly object sync = new object();
        private static IRenderingManager renderingManager;
        private static HtmlRenderer renderer;

        private VsSettings vsSettings;

        [Import]
        private IBufferTagAggregatorFactoryService BufferTagAggregatorFactoryService = null; //MEF

        [Import]
        private IEditorFormatMapService EditorFormatMapService = null; //MEF

        [Import]
        private IVsFontsAndColorsInformationService VsFontsAndColorsInformationService = null; //MEF

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer)
            where T : ITag
        {
            if (textView == null) throw new ArgumentNullException(nameof(textView));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (buffer != textView.TextBuffer) return null;

            var wpfTextView = textView as IWpfTextView;
            if (wpfTextView == null) return null;

            if (!VsSettings.IsInitialized)
            {
                lock (sync)
                {
                    if (!VsSettings.IsInitialized)
                    {
                        VsSettings.Initialize(EditorFormatMapService, VsFontsAndColorsInformationService);
                    }
                }
            }

            if (vsSettings == null)
            {
                lock (sync)
                {
                    if (vsSettings == null)
                    {
                        vsSettings = VsSettings.GetOrCreate(wpfTextView);
                        vsSettings.CommentsColorChanged += ColorsChanged;
                        vsSettings.ZoomChanged += ZoomChanged;
                    }
                }
            }

            var background = vsSettings.GetCommentsBackground();
            var foreground = vsSettings.GetCommentsForeground();
            var font = vsSettings.CommentsFont;

            if (renderingManager == null)
            {
                lock (sync)
                {
                    if (renderingManager == null)
                    {
                        renderer = new HtmlRenderer(wpfTextView.ZoomLevel, background.Color, foreground.Color, font);
                        renderingManager = new RenderingManager(renderer);
                    }
                }
            }

            var resultTagger = TeXCommentAdornmentTagger.GetTagger(
                wpfTextView,
                new Lazy<ITagAggregator<TeXCommentTag>>(
                    () => BufferTagAggregatorFactoryService.CreateTagAggregator<TeXCommentTag>(textView.TextBuffer)),
                    renderingManager,
                    foreground);

            return resultTagger as ITagger<T>;
        }

        private void ZoomChanged(IWpfTextView textView, double zoomPercentage)
        {
            renderer.ZoomScale = 0.01 * zoomPercentage;
        }

        private void ColorsChanged(IWpfTextView textView, SolidColorBrush foreground, SolidColorBrush background)
        {
            renderer.Foreground = foreground.Color;
            renderer.Background = background.Color;
        }

        public void Dispose()
        {
            vsSettings?.Dispose();
        }
    }
}