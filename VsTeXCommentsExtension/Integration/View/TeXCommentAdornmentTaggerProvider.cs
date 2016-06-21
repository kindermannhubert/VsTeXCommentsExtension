using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Windows.Media;
using VsTeXCommentsExtension.Integration.Data;
using VsTeXCommentsExtension.View;

using wpf = System.Windows.Media;

namespace VsTeXCommentsExtension.Integration.View
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [ContentType("projection")]
    [TagType(typeof(IntraTextAdornmentTag))]
    internal sealed class TeXCommentAdornmentTaggerProvider : IViewTaggerProvider, IDisposable
    {
        private static readonly SolidColorBrush DefaultForegroundBrush = new SolidColorBrush(wpf.Color.FromRgb(0, 128, 0));
        private static readonly SolidColorBrush DefaultBackgroundBrush = new SolidColorBrush(Colors.White);
        private static readonly object sync = new object();
        private static IRenderingManager renderingManager;
        private static HtmlRenderer renderer;

        private readonly List<TeXCommentAdornmentTagger> createdTaggers = new List<TeXCommentAdornmentTagger>();
        private readonly List<ObjectEventTuple<IEditorFormatMap, EventHandler<FormatItemsEventArgs>>> editorFormatMapEvents = new List<ObjectEventTuple<IEditorFormatMap, EventHandler<FormatItemsEventArgs>>>();
        private readonly List<ObjectEventTuple<IWpfTextView, EventHandler<BackgroundBrushChangedEventArgs>>> textViewEvents = new List<ObjectEventTuple<IWpfTextView, EventHandler<BackgroundBrushChangedEventArgs>>>();

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
            if (textView == null) throw new ArgumentNullException("textView");
            if (buffer == null) throw new ArgumentNullException("buffer");
            if (buffer != textView.TextBuffer) return null;

            var wpfTextView = textView as IWpfTextView;
            if (wpfTextView == null) return null;

            var editorFormatMap = EditorFormatMapService.GetEditorFormatMap(textView);
            var foregroundColorBrush = GetBrush(editorFormatMap, BrushType.Foreground, wpfTextView, DefaultForegroundBrush);
            var backgroundColorBrush = GetBrush(editorFormatMap, BrushType.Background, wpfTextView, DefaultBackgroundBrush);

            EventHandler<FormatItemsEventArgs> formatMappingChangedHandler = (sender, e) => { ColorsChanged(editorFormatMap, wpfTextView); };
            editorFormatMap.FormatMappingChanged += formatMappingChangedHandler;
            editorFormatMapEvents.Add(new ObjectEventTuple<IEditorFormatMap, EventHandler<FormatItemsEventArgs>>(editorFormatMap, formatMappingChangedHandler));

            EventHandler<BackgroundBrushChangedEventArgs> textViewBackgroundChangedHandler = (sender, e) => { ColorsChanged(editorFormatMap, wpfTextView); };
            wpfTextView.BackgroundBrushChanged += textViewBackgroundChangedHandler;
            textViewEvents.Add(new ObjectEventTuple<IWpfTextView, EventHandler<BackgroundBrushChangedEventArgs>>(wpfTextView, textViewBackgroundChangedHandler));

            if (renderingManager == null)
            {
                lock (sync)
                {
                    if (renderingManager == null)
                    {
                        DefaultForegroundBrush.Freeze();
                        DefaultBackgroundBrush.Freeze();

                        var textEditorFont = LoadTextEditorFont(VsFontsAndColorsInformationService);
                        var backgroundColor = (wpfTextView.Background as SolidColorBrush)?.Color ?? Colors.White;

                        renderer = new HtmlRenderer(TeXCommentAdornment.RenderScale, backgroundColor, foregroundColorBrush.Color, textEditorFont);
                        renderingManager = new RenderingManager(renderer);
                    }
                }
            }

            var resultTagger = TeXCommentAdornmentTagger.GetTagger(
                wpfTextView,
                new Lazy<ITagAggregator<TeXCommentTag>>(
                    () => BufferTagAggregatorFactoryService.CreateTagAggregator<TeXCommentTag>(textView.TextBuffer)),
                    renderingManager,
                    foregroundColorBrush);

            createdTaggers.Add(resultTagger);
            return resultTagger as ITagger<T>;
        }

        private void ColorsChanged(IEditorFormatMap editorFormatMap, IWpfTextView textView)
        {
            var foregroundColorBrush = GetBrush(editorFormatMap, BrushType.Foreground, textView, DefaultForegroundBrush);
            var backgroundColorBrush = GetBrush(editorFormatMap, BrushType.Background, textView, DefaultBackgroundBrush);

            renderer.Foreground = foregroundColorBrush.Color;
            renderer.Background = backgroundColorBrush.Color;

            foreach (var tagger in createdTaggers)
            {
                tagger.CommentsForegroundBrush = foregroundColorBrush;
                tagger.InvalidateAll();
            }
        }

        private static SolidColorBrush GetBrush(IEditorFormatMap editorFormatMap, BrushType type, IWpfTextView textView, SolidColorBrush defaultBrush)
        {
            var props = editorFormatMap.GetProperties("Comment");
            var typeText = type.ToString();

            object value = null;
            if (props.Contains(typeText))
            {
                value = props[typeText];
            }
            else
            {
                typeText += "Color";
                if (props.Contains(typeText))
                {
                    value = props[typeText];
                    if (value is wpf.Color)
                    {
                        var color = (wpf.Color)value;
                        var cb = new SolidColorBrush(color);
                        cb.Freeze();
                        value = cb;
                    }
                }
                else
                {
                    //Background is often not found in editorFormatMap. Don't know why :(
                    if (type == BrushType.Background)
                    {
                        value = textView.Background;
                    }
                }
            }

            return (value as SolidColorBrush) ?? defaultBrush;
        }

        private static Font LoadTextEditorFont(IVsFontsAndColorsInformationService vsFontsAndColorsInformationService)
        {
            try
            {
                //OMG! Isn't there any better way?
                var guidDefaultFileType = new Guid(2184822468u, 61063, 4560, 140, 152, 0, 192, 79, 194, 171, 34); //from Microsoft.VisualStudio.Editor.Implementation.ImplGuidList
                var info = vsFontsAndColorsInformationService.GetFontAndColorInformation(new FontsAndColorsCategory(
                    guidDefaultFileType,
                    DefGuidList.guidTextEditorFontCategory,
                    DefGuidList.guidTextEditorFontCategory));
                //info.Updated - event does not work. Don't know why :(
                var preferences = info.GetFontAndColorPreferences();
                return Font.FromHfont(preferences.hRegularViewFont);
            }
            catch
            {
                return SystemFonts.DefaultFont;
            }
        }

        public void Dispose()
        {
            foreach (var tuple in editorFormatMapEvents)
            {
                tuple.Object.FormatMappingChanged -= tuple.EventHandler;
            }

            foreach (var tuple in textViewEvents)
            {
                tuple.Object.BackgroundBrushChanged -= tuple.EventHandler;
            }
        }

        private enum BrushType
        {
            Foreground,
            Background
        }
    }
}