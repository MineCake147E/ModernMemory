using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public interface ITypedEnumerable<out T, TEnumerator> : IEnumerable<T> where TEnumerator : IEnumerator<T>
    {
        new TEnumerator GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public interface IMemoryEnumerable<T> : ITypedEnumerable<T, ReadOnlyNativeMemory<T>.Enumerator>
    {
    }

    public interface ISpanEnumerable<T>
    {
        ReadOnlyNativeSpan<T>.Enumerator GetEnumerator();
    }
}
