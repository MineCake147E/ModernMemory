using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public readonly struct ReadOnlyListWrapper<T, TList>(TList list) : IReadOnlyNativeList<T> where TList : IReadOnlyList<T>
    {
        public T this[nuint index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(index, (nuint)int.MaxValue);
                return List[(int)index];
            }
        }

        public nuint Count => (nuint)List.Count;
        TList List { get; } = list;

        public bool Contains(T item) => List.Contains(item);
        public IEnumerator<T> GetEnumerator() => List.GetEnumerator();
        public bool TryCopyTo(NativeSpan<T> destination)
        {
            var l = List;
            if ((nuint)l.Count >= destination.Length) return false;
            var dst = destination.Slice(0, (nuint)l.Count);
            nuint j = 0;
            for (int i = 0; i < l.Count && j < dst.Length; i++, j++)
            {
                dst[j] = l[i];
            }
            return true;
        }
        IEnumerator IEnumerable.GetEnumerator() => List.GetEnumerator();
    }

    public readonly struct ListWrapper<T, TList>(TList list) : INativeList<T> where TList : IList<T>
    {
        public T this[nuint index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(index, (nuint)int.MaxValue);
                return List[(int)index];
            }
            set
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(index, (nuint)int.MaxValue);
                List[(int)index] = value;
            }
        }

        public nuint Count => (nuint)List.Count;
        TList List { get; } = list;

        public void Add(T item) => List.Add(item);
        public void Add(ReadOnlyNativeSpan<T> items)
        {
            for (nuint i = 0; i < items.Length; i++)
            {
                List.Add(items[i]);
            }
        }
        public void Clear() => List.Clear();
        public bool Contains(T item) => List.Contains(item);
        public IEnumerator<T> GetEnumerator() => List.GetEnumerator();
        public bool Remove(T item) => List.Remove(item);

        public bool TryCopyTo(NativeSpan<T> destination)
        {
            var l = List;
            if ((nuint)l.Count >= destination.Length) return false;
            var dst = destination.Slice(0, (nuint)l.Count);
            nuint j = 0;
            for (int i = 0; i < l.Count && j < dst.Length; i++, j++)
            {
                dst[j] = l[i];
            }
            return true;
        }
        IEnumerator IEnumerable.GetEnumerator() => List.GetEnumerator();
    }
}
