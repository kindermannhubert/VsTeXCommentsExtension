using System.Windows.Media;

namespace VsTeXCommentsExtension.View.DesignTimeContexts
{
    internal class DesignVsSettings : IVsSettings
    {
        public SolidColorBrush CommentsForeground => Brushes.Green;
        public SolidColorBrush CommentsBackground => Brushes.White;
        public System.Drawing.Font CommentsFont => new System.Drawing.Font("Consolas", 12);
        public double ZoomPercentage => 100;

#pragma warning disable CS0067
        public event VsSettings.CommentsColorChangedHandler CommentsColorChanged;
        public event VsSettings.ZoomChangedHandler ZoomChanged;
#pragma warning restore CS0067
    }
}
