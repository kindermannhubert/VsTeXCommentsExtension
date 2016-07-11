namespace VsTeXCommentsExtension.Integration.View
{
    public interface ITagAdornment
    {
        IntraTextAdornmentTaggerDisplayMode DisplayMode { get; }
        TeXCommentAdornmentState CurrentState { get; set; }
        bool IsInEditMode { get; }
        void Invalidate();
        int Index { get; }
    }
}
