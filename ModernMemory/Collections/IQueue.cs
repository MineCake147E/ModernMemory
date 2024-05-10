using System.Buffers;
using System.Diagnostics.CodeAnalysis;

using ModernMemory.DataFlow;

namespace ModernMemory.Collections
{
    public interface IQueue<T> : IClearable<T>, IReadOnlyNativeIndexable<T>, IAddable<T>
    {
        T Dequeue();
        void DequeueAll<TBufferWriter>(ref TBufferWriter writer) where TBufferWriter : IBufferWriter<T>;
        void DequeueRange<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>;
        void DequeueRange<TBufferWriter>(ref TBufferWriter writer, nuint elements) where TBufferWriter : IBufferWriter<T>;
        void DequeueRangeExact(NativeSpan<T> destination);
        nuint DequeueRangeAtMost(NativeSpan<T> destination);
        nuint DequeueRangeAtMost<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>;
        void DiscardHead(nuint count);
        nuint DiscardHeadAtMost(nuint count);
        T Peek();
        bool TryDequeue([MaybeNullWhen(false)] out T item);
        bool TryPeek([MaybeNullWhen(false)] out T item);
    }
}