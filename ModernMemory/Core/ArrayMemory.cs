using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Core
{
    public readonly struct ArrayMemory<T> : INativeMemory<T, ArrayMemory<T>>
    {
        internal readonly ArraySegment<T> arraySegment;

        public ArrayMemory(ArraySegment<T> arraySegment)
        {
            this.arraySegment = arraySegment;
        }

        public ArrayMemory(T[] array)
        {
            this = new(new ArraySegment<T>(array));
        }

        public ArrayMemory(T[] array, int start)
        {
            this = new(new ArraySegment<T>(array).Slice(start));
        }

        public ArrayMemory(T[] array, int start, int length)
        {
            this = new(new ArraySegment<T>(array, start, length));
        }

        /// <summary>
        /// Gets the number of items in the current instance.
        /// </summary>
        /// <internalValue>The number of items in the current instance.</internalValue>
        public nuint Length => (nuint)arraySegment.Count;

        public NativeSpan<T> Span => arraySegment.AsNativeSpan();

        public NativeMemory<T> AsNativeMemory() => new(arraySegment);
        public void CopyTo(NativeSpan<T> destination) => Span.CopyTo(destination);
        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => Span.GetEnumerator();
        public MemoryHandle Pin() => arraySegment.AsNativeMemory().Pin();
        public ArrayMemory<T> Slice(nuint start) => new(arraySegment.Slice(checked((int)start)));
        public ArrayMemory<T> Slice(nuint start, nuint length) => new(arraySegment.Slice(checked((int)start), checked((int)length)));
        public bool TryCopyTo(NativeSpan<T> destination) => Span.TryCopyTo(destination);
    }
}
