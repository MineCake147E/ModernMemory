using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Collections
{
    public interface IReadOnlyNativeCollection<T> : IEnumerable<T>, ICountable<nuint>
    {
        bool Contains(T item) => ((IEnumerable<T>)this).Contains(item);
        bool TryCopyTo(NativeSpan<T> destination)
        {
            var count = Count;
            if (destination.Length < count) return false;
            using var enumerator = GetEnumerator();
            if (enumerator is null) return false;
            for (nuint i = 0; i < destination.Length && enumerator.MoveNext(); i++)
            {
                destination[i] = enumerator.Current;
            }
            return true;
        }
    }
    public interface INativeCollection<T> : IReadOnlyNativeCollection<T>, IAddableCollection<T>, IClearable<T>
    {
    }
}
