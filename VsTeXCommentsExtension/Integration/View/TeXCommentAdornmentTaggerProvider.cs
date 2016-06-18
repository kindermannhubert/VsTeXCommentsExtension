using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Drawing;
using VsTeXCommentsExtension.Integration.Data;

namespace VsTeXCommentsExtension.Integration.View
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [ContentType("projection")]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class TeXCommentAdornmentTaggerProvider : IViewTaggerProvider
    {
        private static readonly object sync = new object();
        private static Font textEditorFont;

#pragma warning disable 649 // "field never assigned to" -- field is set by MEF.
        [Import]
        internal IBufferTagAggregatorFactoryService BufferTagAggregatorFactoryService;

        [Import]
        internal IEditorFormatMapService EditorFormatMapService;

        [Import]
        internal IVsFontsAndColorsInformationService VsFontsAndColorsInformationService;
#pragma warning restore 649

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textEditorFont == null)
            {
                lock (sync)
                {
                    if (textEditorFont == null)
                    {
                        textEditorFont = LoadTextEditorFont(VsFontsAndColorsInformationService);
                    }
                }
            }

            if (textView == null)
                throw new ArgumentNullException("textView");

            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (buffer != textView.TextBuffer)
                return null;

            return TeXCommentAdornmentTagger.GetTagger(
                (IWpfTextView)textView,
                new Lazy<ITagAggregator<TeXCommentTag>>(
                    () => BufferTagAggregatorFactoryService.CreateTagAggregator<TeXCommentTag>(textView.TextBuffer)),
                EditorFormatMapService,
                textEditorFont)
                as ITagger<T>;
        }

        private static Font LoadTextEditorFont(IVsFontsAndColorsInformationService vsFontsAndColorsInformationService)
        {
            //OMG!
            var guidDefaultFileType = new Guid(2184822468u, 61063, 4560, 140, 152, 0, 192, 79, 194, 171, 34); //from Microsoft.VisualStudio.Editor.Implementation.ImplGuidList
            var info = vsFontsAndColorsInformationService.GetFontAndColorInformation(new FontsAndColorsCategory(
                guidDefaultFileType,
                DefGuidList.guidTextEditorFontCategory,
                DefGuidList.guidTextEditorFontCategory));
            var preferences = info.GetFontAndColorPreferences();
            return Font.FromHfont(preferences.hRegularViewFont);
        }
    }
}
