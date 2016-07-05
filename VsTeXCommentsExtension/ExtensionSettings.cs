namespace VsTeXCommentsExtension
{
    public class ExtensionSettings
    {
        public static ExtensionSettings Instance { get; } = new ExtensionSettings();

        private double customZoomScale = 1;
        public double CustomZoomScale
        {
            get { return customZoomScale; }
            set
            {
                if (customZoomScale != value)
                {
                    customZoomScale = value;
                    CustomZoomChanged?.Invoke(value);
                }
            }
        }

        public event ZoomChangedHandler CustomZoomChanged;

        private ExtensionSettings()
        {
        }

        public delegate void ZoomChangedHandler(double zoomScale);
    }
}
