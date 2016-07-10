using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View
{
    public class TeXCommentAdornmentDesignDataContext
    {
        public TeXCommentAdornmentState CurrentState => TeXCommentAdornmentState.Rendering;

        public IResourcesManager ResourcesManager { get; } = new DesignResourcesManager();

        public IVsSettings VsSettings { get; } = new DesignVsSettings();

        private class DesignResourcesManager : IResourcesManager
        {
            public Color ForegroundUIColor => Colors.Black;
            public Color BackgroundUIColor => Colors.White;
            public ImageSource DropDown { get; } = new BitmapImage(View.ResourcesManager.GetAssemblyResourceUri("DropDown_Dark.png"));
            public ImageSource Edit { get; } = new BitmapImage(View.ResourcesManager.GetAssemblyResourceUri("Edit_Dark.png"));
            public ImageSource Show { get; } = new BitmapImage(View.ResourcesManager.GetAssemblyResourceUri("Show_Dark.png"));
            public ImageSource Warning { get; } = new BitmapImage(View.ResourcesManager.GetAssemblyResourceUri("Warning_Dark.png"));
        }

        private class DesignVsSettings : IVsSettings
        {
            public SolidColorBrush CommentsForeground => Brushes.Green;
            public SolidColorBrush CommentsBackground => Brushes.White;
            public System.Drawing.Font CommentsFont => new System.Drawing.Font("Consolas", 12);
            public double ZoomPercentage => 100;

            public event VsSettings.CommentsColorChangedHandler CommentsColorChanged;
            public event VsSettings.ZoomChangedHandler ZoomChanged;
        }
    }
}
