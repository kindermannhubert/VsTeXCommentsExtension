using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Media;

using wpf = System.Windows.Media;

namespace VsTeXCommentsExtension
{
    public class VisualStudioSettings
    {
        private static readonly SolidColorBrush DefaultForegroundBrush = new SolidColorBrush(wpf.Color.FromRgb(0, 128, 0));
        private static readonly SolidColorBrush DefaultBackgroundBrush = new SolidColorBrush(Colors.White);

        public static VisualStudioSettings Instance { get; } = new VisualStudioSettings();

        private readonly Dictionary<IWpfTextView, IEditorFormatMap> textViewEditorFormatMapMapping = new Dictionary<IWpfTextView, IEditorFormatMap>();
        private IEditorFormatMapService editorFormatMapService;
        private IVsFontsAndColorsInformationService vsFontsAndColorsInformationService;

        public bool IsInitialized { get; private set; }
        public Font CommentsFont { get; private set; }

        public event CommentsColorChangedHandler CommentsColorChanged;
        public event ZoomChangedHandler ZoomChanged;

        private VisualStudioSettings()
        {
        }

        public void Initialize(IEditorFormatMapService editorFormatMapService, IVsFontsAndColorsInformationService vsFontsAndColorsInformationService)
        {
            if (IsInitialized)
                throw new InvalidOperationException($"{nameof(VisualStudioSettings)} class is already initialized.");

            IsInitialized = true;

            DefaultForegroundBrush.Freeze();
            DefaultBackgroundBrush.Freeze();

            this.editorFormatMapService = editorFormatMapService;
            this.vsFontsAndColorsInformationService = vsFontsAndColorsInformationService;

            CommentsFont = LoadTextEditorFont(vsFontsAndColorsInformationService);
        }

        public SolidColorBrush GetCommentsForeground(IWpfTextView textView) => GetBrush(editorFormatMapService.GetEditorFormatMap(textView), BrushType.Foreground, textView);
        public SolidColorBrush GetCommentsBackground(IWpfTextView textView) => GetBrush(editorFormatMapService.GetEditorFormatMap(textView), BrushType.Background, textView);

        public void RegisterForEventsListening(IWpfTextView textView)
        {
            var editorFormatMap = editorFormatMapService.GetEditorFormatMap(textView);
            textViewEditorFormatMapMapping.Add(textView, editorFormatMap);

            editorFormatMap.FormatMappingChanged += OnFormatItemsChanged;
            textView.BackgroundBrushChanged += OnBackgroundBrushChanged;
            textView.ZoomLevelChanged += OnZoomChanged;
        }

        public void UnregisterFromEventsListening(IWpfTextView textView)
        {
            var editorFormatMap = textViewEditorFormatMapMapping[textView];
            textViewEditorFormatMapMapping.Remove(textView);

            editorFormatMap.FormatMappingChanged -= OnFormatItemsChanged;
            textView.BackgroundBrushChanged -= OnBackgroundBrushChanged;
            textView.ZoomLevelChanged -= OnZoomChanged;
        }

        private void OnFormatItemsChanged(object sender, FormatItemsEventArgs args)
        {
            if (args.ChangedItems.Any(i => i == "Comment"))
            {
                foreach (var textView in textViewEditorFormatMapMapping.Where(kv => kv.Value == sender).Select(kv => kv.Key).Distinct())
                {
                    var editorFormatMap = (IEditorFormatMap)sender;
                    CommentsColorChanged?.Invoke(
                        textView,
                        GetBrush(editorFormatMap, BrushType.Foreground, textView),
                        GetBrush(editorFormatMap, BrushType.Background, textView));
                }
            }
        }

        private void OnBackgroundBrushChanged(object sender, BackgroundBrushChangedEventArgs args)
        {
            var textView = (IWpfTextView)sender;
            var editorFormatMap = textViewEditorFormatMapMapping[textView];
            CommentsColorChanged?.Invoke(
                textView,
                GetBrush(editorFormatMap, BrushType.Foreground, textView),
                GetBrush(editorFormatMap, BrushType.Background, textView));
        }

        private void OnZoomChanged(object sender, ZoomLevelChangedEventArgs args)
        {
            ZoomChanged?.Invoke((IWpfTextView)sender, args.NewZoomLevel);
        }

        private static SolidColorBrush GetBrush(IEditorFormatMap editorFormatMap, BrushType type, IWpfTextView textView)
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

            return (value as SolidColorBrush) ?? (type == BrushType.Background ? DefaultBackgroundBrush : DefaultForegroundBrush);
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

        public delegate void CommentsColorChangedHandler(IWpfTextView textView, SolidColorBrush foreground, SolidColorBrush background);
        public delegate void ZoomChangedHandler(IWpfTextView textView, double zoom);

        private enum BrushType
        {
            Foreground,
            Background
        }
    }
}
