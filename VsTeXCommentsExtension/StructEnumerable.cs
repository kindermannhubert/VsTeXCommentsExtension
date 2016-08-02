using System.Collections.Generic;

namespace VsTeXCommentsExtension
{
    public struct StructEnumerable<T>
    {
        private readonly List<T> list;

        public StructEnumerable(List<T> list)
        {
            this.list = list;
        }

        public List<T>.Enumerator GetEnumerator() => list.GetEnumerator();

        public int Count => list.Count;

        public T this[int index] => list[index];
    }
}
