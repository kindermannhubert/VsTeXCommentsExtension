namespace VsTeXCommentsExtension
{
    public struct ObjectEventTuple<TObject, THandler>
    {
        public readonly TObject Object;
        public readonly THandler EventHandler;

        public ObjectEventTuple(TObject obj, THandler eventHandler)
        {
            Object = obj;
            EventHandler = eventHandler;
        }
    }
}
