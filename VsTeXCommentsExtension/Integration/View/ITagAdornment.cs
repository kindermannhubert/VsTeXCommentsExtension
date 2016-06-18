namespace VsTeXCommentsExtension.Integration.View
{
    internal interface ITagAdornment
    {
        IntraTextAdornmentTaggerDisplayMode DisplayMode { get; }
        int DebugIndex { get; }
    }
}
