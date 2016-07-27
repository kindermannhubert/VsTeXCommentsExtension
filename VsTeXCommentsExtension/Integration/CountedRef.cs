using System;
using System.Diagnostics;

namespace VsTeXCommentsExtension.Integration
{
    internal class CountedRef<T>
        where T : class, IDisposable
    {
        private readonly Func<T> createValue;
        private T value;
        private int count;

        public CountedRef(Func<T> createValue)
        {
            this.createValue = createValue;
            count = 1;
        }

        public T GetOrCreate()
        {
            lock (createValue)
            {
                if (value == null)
                {
                    value = createValue();
                }

                ++count;
                return value;
            }
        }

        public void Release()
        {
            lock (createValue)
            {
                --count;
                Debug.Assert(count >= 0);

                if (count == 0)
                {
                    value.Dispose();
                    value = null;
                }
            }
        }
    }
}
