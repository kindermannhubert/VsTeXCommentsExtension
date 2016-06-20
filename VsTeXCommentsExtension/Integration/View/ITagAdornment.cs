namespace VsTeXCommentsExtension.Integration.View
{
    internal interface ITagAdornment
    {
        IntraTextAdornmentTaggerDisplayMode DisplayMode { get; }
        bool IsInEditMode { get; set; }
        int DebugIndex { get; }
    }
}
