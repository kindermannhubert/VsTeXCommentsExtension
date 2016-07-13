using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View.DesignTimeContexts
{
    internal class DesignResourcesManager : IResourcesManager
    {
        public SolidColorBrush ForegroundUI => new SolidColorBrush(Colors.Black);
        public SolidColorBrush BackgroundUI => new SolidColorBrush(Colors.White);
        public ImageSource DropDown { get; } = new BitmapImage(View.ResourcesManager.GetAssemblyResourceUri("DropDown_Dark.png"));
        public ImageSource Edit { get; } = new BitmapImage(View.ResourcesManager.GetAssemblyResourceUri("Edit_Dark.png"));
        public ImageSource Show { get; } = new BitmapImage(View.ResourcesManager.GetAssemblyResourceUri("Show_Dark.png"));
        public ImageSource Warning { get; } = new BitmapImage(View.ResourcesManager.GetAssemblyResourceUri("Warning_Dark.png"));
    }
}
