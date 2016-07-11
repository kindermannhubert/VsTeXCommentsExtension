using System.Windows.Media;
using System.Windows.Media.Imaging;
using VsTeXCommentsExtension.Integration.View;

namespace VsTeXCommentsExtension.View.DesignTimeContexts
{
    public class TeXCommentAdornmentDesignContext
    {
        //Change and recompile to see change in designer.
        public TeXCommentAdornmentState CurrentState => TeXCommentAdornmentState.EditingAndRenderingPreview;

        public IResourcesManager ResourcesManager { get; } = new DesignResourcesManager();

        public IVsSettings VsSettings { get; } = new DesignVsSettings();

        public ImageSource RenderedImage { get; } = new BitmapImage(View.ResourcesManager.GetAssemblyResourceUri("DesignPreview.png"));

        public bool AnyRenderingErrors => CurrentState == TeXCommentAdornmentState.Rendering;

        public string ErrorsSummary => "some error";

        public double RenderedImageWidth => 271 / 1.5;

        public double RenderedImageHeight => 29 / 1.5;
    }
}
