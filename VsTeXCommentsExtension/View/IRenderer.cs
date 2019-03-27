namespace VsTeXCommentsExtension.View
{
    public interface IRenderer<TInput, TResult>
        where TInput : IRendererInput
    {
        TResult Render(TInput input);
    }
}