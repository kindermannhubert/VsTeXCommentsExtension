namespace VsTeXCommentsExtension.Integration.View
{
    internal interface ITagAdornment
    {
        IntraTextAdornmentTaggerDisplayMode DisplayMode { get; }
        bool IsInEditMode { get; set; }
        void Invalidate();
        int DebugIndex { get; }
    }
}
