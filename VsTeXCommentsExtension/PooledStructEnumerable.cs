using System;
using System.Collections.Generic;

namespace VsTeXCommentsExtension
{
    public struct PooledStructEnumerable<T> : IDisposable
    {
        private readonly List<T> list;
        private readonly ObjectPool<List<T>> sourcePool;
        private bool isDisposed;

        public PooledStructEnumerable(List<T> list, ObjectPool<List<T>> sourcePool)
        {
            this.list = list;
            this.sourcePool = sourcePool;
            isDisposed = false;
        }

        public void Dispose()
        {
            if (isDisposed) return;
            list.Clear();
            sourcePool.Put(list);
            isDisposed = true;
        }

        public List<T>.Enumerator GetEnumerator() => list.GetEnumerator();

        public int Count => list.Count;

        public T this[int index] => list[index];

        public static implicit operator StructEnumerable<T>(PooledStructEnumerable<T> value) => new StructEnumerable<T>(value.list);
    }
}