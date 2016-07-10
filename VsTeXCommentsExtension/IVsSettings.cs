using System.Drawing;
using System.Windows.Media;

namespace VsTeXCommentsExtension
{
    public interface IVsSettings
    {
        SolidColorBrush CommentsBackground { get; }
        Font CommentsFont { get; }
        SolidColorBrush CommentsForeground { get; }
        double ZoomPercentage { get; }

        event VsSettings.CommentsColorChangedHandler CommentsColorChanged;
        event VsSettings.ZoomChangedHandler ZoomChanged;
    }
}