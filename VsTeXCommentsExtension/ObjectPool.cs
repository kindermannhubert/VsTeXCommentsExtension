using System;
using System.Collections.Generic;

namespace VsTeXCommentsExtension
{
    public class ObjectPool<T>
        where T : class
    {
        private readonly Func<T> createValue;
        private readonly List<T> values = new List<T>();

        public ObjectPool(Func<T> createValue)
        {
            this.createValue = createValue;
        }

        public T Get()
        {
            if (values.Count > 0)
            {
                var index = values.Count - 1;
                var val = values[index];
                values.RemoveAt(index);
                return val;
            }
            else
            {
                return createValue();
            }
        }

        public void Put(T value)
        {
            values.Add(value);
        }
    }
}