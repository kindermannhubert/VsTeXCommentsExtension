using System.Windows.Media;

namespace VsTeXCommentsExtension.View.DesignTimeContexts
{
    public class DropDownImageButtonDesignContext
    {
        public IResourcesManager ResourcesManager { get; } = new DesignResourcesManager();
        public ImageSource ImageSource { get; }

        public DropDownImageButtonDesignContext()
        {
            ImageSource = ResourcesManager.Edit;
        }
    }
}
