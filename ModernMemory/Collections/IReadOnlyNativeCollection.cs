using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Collections
{
    public interface IReadOnlyNativeCollection<T> : IEnumerable<T>
    {
        nuint Count { get; }
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
    public interface INativeCollection<T> : IReadOnlyNativeCollection<T>
    {
        void Add(T item);
        void Add(ReadOnlySpan<T> items) => Add((ReadOnlyNativeSpan<T>)items);
        void Add(ReadOnlyNativeSpan<T> items)
        {
            for (nuint i = 0; i < items.Length; i++)
            {
                Add(items[i]);
            }
        }
        void Add<TReadOnlyList>(TReadOnlyList items) where TReadOnlyList : IReadOnlyNativeList<T>
        {
            for (nuint i = 0; i < items.Count; i++)
            {
                Add(items[i]);
            }
        }
        void AddRange<TEnumerable>(TEnumerable collection) where TEnumerable : IEnumerable<T>
        {
            foreach (var item in collection)
            {
                Add(item);
            }
        }
        void AddList<TReadOnlyList>(TReadOnlyList items) where TReadOnlyList : IReadOnlyList<T>
        {
            for (int i = 0; i < items.Count; i++)
            {
                Add(items[i]);
            }
        }
        void Clear();
        bool Remove(T item);
    }
}
