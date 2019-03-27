using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media.Imaging;

namespace VsTeXCommentsExtension.View
{
    public readonly struct RendererResult
    {
        public readonly BitmapSource Image;

        /// <summary>
        /// Can be null when there was some errors while rendering.
        /// </summary>
        public readonly string CachePath;

        public readonly IReadOnlyList<string> Errors;

        public RendererResult(BitmapSource image, string cachePath, IReadOnlyList<string> errors)
        {
            Debug.Assert(image != null);
            Debug.Assert(errors != null);

            Image = image;
            CachePath = cachePath;
            Errors = errors;
        }

        public bool HasErrors => Errors.Count > 0;

        public string ErrorsSummary => Errors.Count > 0 ? Errors.Aggregate((a, b) => $"{a}\r\n{b}") : string.Empty;
    }
}