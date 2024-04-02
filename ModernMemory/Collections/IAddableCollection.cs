using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Collections
{
    public interface IAddableCollection<T>
    {
        void EnsureCapacityToAdd(nuint size) { }

        void Add(T item);
        void Add(ReadOnlySpan<T> items) => Add((ReadOnlyNativeSpan<T>)items);
        void Add(ReadOnlyNativeSpan<T> items);
        void Add(ReadOnlySequenceSlim<T> items)
        {
            EnsureCapacityToAdd(items.Length);
            foreach (var segment in items.GetSegmentsEnumerable())
            {
                Add(segment.Span);
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
    }
}
