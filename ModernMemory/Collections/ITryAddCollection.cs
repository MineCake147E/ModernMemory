using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Collections
{
    public interface ITryAddCollection<T>
    {
        nuint ResizeToAddAtMost(nuint size);
        bool TryAdd(T item);
        nuint AddAtMost(ReadOnlyNativeSpan<T> items);
        nuint AddAtMost(ReadOnlySequenceSlim<T> items)
        {
            ResizeToAddAtMost(items.Length);
            nuint c = 0;
            foreach (var segment in items.GetSegmentsEnumerable())
            {
                var span = segment.Span;
                var added = AddAtMost(span);
                if (added < span.Length) break;
            }
            return c;
        }

        nuint AddAtMost<TReadOnlyList>(TReadOnlyList items) where TReadOnlyList : IReadOnlyNativeList<T>
        {
            nuint i;
            for (i = 0; i < items.Count; i++)
            {
                if (!TryAdd(items[i])) break;
            }
            return i;
        }

        nuint AddRangeAtMost(ReadOnlySpan<T> items) => AddAtMost((ReadOnlyNativeSpan<T>)items);
        nuint AddRangeAtMost<TEnumerable>(TEnumerable collection) where TEnumerable : IEnumerable<T>
        {
            nuint c = 0;
            foreach (var item in collection)
            {
                if (!TryAdd(item)) break;
                c++;
            }
            return c;
        }
        nuint AddListAtMost<TReadOnlyList>(TReadOnlyList items) where TReadOnlyList : IReadOnlyList<T>
        {
            int i;
            for (i = 0; i < items.Count; i++)
            {
                if (!TryAdd(items[i])) break;
            }
            return (nuint)i;
        }
    }
}
