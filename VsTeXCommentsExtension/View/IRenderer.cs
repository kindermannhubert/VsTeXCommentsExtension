namespace VsTeXCommentsExtension.View
{
    public interface IRenderer<TInput, TResult>
    {
        TResult Render(TInput input);
    }
}
