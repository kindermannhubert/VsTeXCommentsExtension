namespace VsTeXCommentsExtension.View
{
    public interface IRenderer<TResult>
    {
        TResult Render(string content);
    }
}
