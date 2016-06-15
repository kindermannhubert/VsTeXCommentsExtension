namespace VsTeXCommentsExtension
{
    internal interface ITagAdornment
    {
        IntraTextAdornmentTaggerDisplayMode DisplayMode { get; }
        int DebugIndex { get; }
    }
}
