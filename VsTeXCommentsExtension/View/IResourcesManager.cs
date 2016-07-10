using System.Windows.Media;

namespace VsTeXCommentsExtension.View
{
    public interface IResourcesManager
    {
        Color BackgroundUIColor { get; }
        ImageSource DropDown { get; }
        ImageSource Edit { get; }
        Color ForegroundUIColor { get; }
        ImageSource Show { get; }
        ImageSource Warning { get; }
    }
}