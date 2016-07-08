using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.View;

namespace VsTeXCommentsExtension.Integration.View
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [ContentType("projection")]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class TeXCommentAdornmentTaggerProvider : IViewTaggerProvider
    {
        private static readonly object sync = new object();
        private static IRenderingManager renderingManager;

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

            if (renderingManager == null)
            {
                lock (sync)
                {
                    if (renderingManager == null)
                    {
                        renderingManager = new RenderingManager(new HtmlRenderer());
                    }
                }
            }

            var resultTagger = TeXCommentAdornmentTagger.GetTagger(
                wpfTextView,
                new Lazy<ITagAggregator<TeXCommentTag>>(
                    () => BufferTagAggregatorFactoryService.CreateTagAggregator<TeXCommentTag>(textView.TextBuffer)),
                    renderingManager);

            return resultTagger as ITagger<T>;
        }
    }
}