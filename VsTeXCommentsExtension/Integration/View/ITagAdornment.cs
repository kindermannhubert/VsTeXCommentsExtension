using System;

namespace VsTeXCommentsExtension.Integration.View
{
    public interface ITagAdornment : IDisposable
    {
        IntraTextAdornmentTaggerDisplayMode DisplayMode { get; }
        TeXCommentAdornmentState CurrentState { get; set; }
        bool IsInEditMode { get; }
        void Invalidate();
        int Index { get; }

        event EventHandler DisplayModeChanged;
    }
}