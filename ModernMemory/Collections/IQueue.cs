using System.Buffers;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;

using ModernMemory.DataFlow;

namespace ModernMemory.Collections
{
    public interface IQueue<T> : IClearable<T>, IReadOnlyNativeIndexable<T>, IAddable<T>
    {
        T? Dequeue();
        void DequeueAll<TBufferWriter>(ref TBufferWriter writer) where TBufferWriter : IBufferWriter<T>;
        void DequeueRange<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>;
        void DequeueRange<TBufferWriter>(ref TBufferWriter writer, nuint elements) where TBufferWriter : IBufferWriter<T>;
        void DequeueRangeExact(NativeSpan<T> destination);
        nuint DequeueRangeAtMost(NativeSpan<T> destination);
        nuint DequeueRangeAtMost<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>;
        void DiscardHead(nuint count);
        nuint DiscardHeadAtMost(nuint count);
        T? Peek();
        /// <summary>
        /// Removes the item at the beginning of the <see cref="IQueue{T}"/> and copies it to the <paramref name="item"/>.
        /// </summary>
        /// <param name="item">The removed item if succeeded, or <see cref="Unsafe.SkipInit{T}(out T)"/> otherwise.</param>
        /// <returns><see langword="true"/> if the item is successfully removed, <see langword="false"/> if the <see cref="IQueue{T}"/> object somehow failed to remove an item.</returns>
        bool TryDequeue(out T? item);
        bool TryPeek(out T? item);
    }
}