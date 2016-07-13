using System.Windows.Media;

namespace VsTeXCommentsExtension.View
{
    public interface IResourcesManager
    {
        SolidColorBrush BackgroundUI { get; }
        ImageSource DropDown { get; }
        ImageSource Edit { get; }
        SolidColorBrush ForegroundUI { get; }
        ImageSource Show { get; }
        ImageSource Warning { get; }
    }
}