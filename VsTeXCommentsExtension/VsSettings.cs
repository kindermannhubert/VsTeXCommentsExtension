using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Media;

using wpf = System.Windows.Media;

namespace VsTeXCommentsExtension
{
    public class VsSettings : IDisposable, INotifyPropertyChanged, IVsSettings
    {
        private static readonly SolidColorBrush DefaultForegroundBrush = new SolidColorBrush(wpf.Color.FromRgb(0, 128, 0));
        private static readonly SolidColorBrush DefaultBackgroundBrush = new SolidColorBrush(Colors.White);
        private static readonly Dictionary<IWpfTextView, VsSettings> Instances = new Dictionary<IWpfTextView, VsSettings>();

        public static bool IsInitialized { get; private set; }
        private static IEditorFormatMapService editorFormatMapService;
        private static IVsFontsAndColorsInformationService vsFontsAndColorsInformationService;

        private readonly IWpfTextView textView;
        private IEditorFormatMap editorFormatMap;

        public Font CommentsFont { get; }

        private SolidColorBrush commentsForeground;
        public SolidColorBrush CommentsForeground
        {
            get { return commentsForeground; }
            private set
            {
                commentsForeground = value;
                OnPropertyChanged(nameof(CommentsForeground));
            }
        }

        private SolidColorBrush commentsBackground;
        public SolidColorBrush CommentsBackground
        {
            get { return commentsBackground; }
            private set
            {
                commentsBackground = value;
                OnPropertyChanged(nameof(CommentsBackground));
            }
        }

        public double ZoomPercentage => textView.ZoomLevel;

        public event CommentsColorChangedHandler CommentsColorChanged;
        public event ZoomChangedHandler ZoomChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public static VsSettings GetOrCreate(IWpfTextView textView)
        {
            lock (Instances)
            {
                if (!Instances.TryGetValue(textView, out VsSettings settings))
                {
                    settings = new VsSettings(textView);
                    Instances.Add(textView, settings);
                }
                return settings;
            }
        }

        public static void Initialize(IEditorFormatMapService editorFormatMapService, IVsFontsAndColorsInformationService vsFontsAndColorsInformationService)
        {
            if (IsInitialized)
                throw new InvalidOperationException($"{nameof(VsSettings)} class is already initialized.");

            IsInitialized = true;

            DefaultForegroundBrush.Freeze();
            DefaultBackgroundBrush.Freeze();

            VsSettings.editorFormatMapService = editorFormatMapService;
            VsSettings.vsFontsAndColorsInformationService = vsFontsAndColorsInformationService;
        }

        private VsSettings(IWpfTextView textView)
        {
            Debug.Assert(IsInitialized);

            this.textView = textView;
            editorFormatMap = editorFormatMapService.GetEditorFormatMap(textView);
            CommentsFont = LoadTextEditorFont(vsFontsAndColorsInformationService);
            ReloadColors();

            editorFormatMap.FormatMappingChanged += OnFormatItemsChanged;
            textView.BackgroundBrushChanged += OnBackgroundBrushChanged;
            textView.ZoomLevelChanged += OnZoomChanged;
        }

        private void ReloadColors()
        {
            CommentsForeground = GetBrush(editorFormatMap, BrushType.Foreground, textView);
            CommentsBackground = GetBrush(editorFormatMap, BrushType.Background, textView);
        }

        private void OnFormatItemsChanged(object sender, FormatItemsEventArgs args)
        {
            if (args.ChangedItems.Any(i => i == "Comment"))
            {
                ReloadColors();
                CommentsColorChanged?.Invoke(textView, CommentsForeground, CommentsBackground);
            }
        }

        private void OnBackgroundBrushChanged(object sender, BackgroundBrushChangedEventArgs args)
        {
            ReloadColors();
            CommentsColorChanged?.Invoke(textView, CommentsForeground, CommentsBackground);
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

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void Dispose()
        {
            editorFormatMap.FormatMappingChanged -= OnFormatItemsChanged;
            textView.BackgroundBrushChanged -= OnBackgroundBrushChanged;
            textView.ZoomLevelChanged -= OnZoomChanged;

            foreach (CommentsColorChangedHandler method in CommentsColorChanged.GetInvocationList()) CommentsColorChanged -= method;
            foreach (ZoomChangedHandler method in ZoomChanged.GetInvocationList()) ZoomChanged -= method;
        }

        public delegate void CommentsColorChangedHandler(IWpfTextView textView, SolidColorBrush foreground, SolidColorBrush background);
        public delegate void ZoomChangedHandler(IWpfTextView textView, double zoomPercentage);

        private enum BrushType
        {
            Foreground,
            Background
        }
    }
}
