using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Collections.Concurrent;
using ModernMemory.Collections.Storage;

namespace ModernMemory.Collections
{
    internal static class NativeCollectionBuilder
    {
        internal static NativeQueueCore<T> CreateCore<T>(ReadOnlySpan<T> span) => new(span);

        internal static NativeQueue<T> CreateNativeQueue<T>(ReadOnlySpan<T> span) => new(CreateCore(span));

        internal static BlockingNativeQueue<T> CreateBlockingNativeQueue<T>(ReadOnlySpan<T> span) => new(CreateCore(span));

        internal static BoundedNativeRingQueue<T, ArrayStorage<T>> CreateBoundedNativeRingQueue<T>(ReadOnlySpan<T> span) => new(new(span.Length), span);

        internal static BoundedNativeRingQueue2<T> CreateBoundedNativeRingQueue2<T>(ReadOnlySpan<T> span) => new(new(NativeMemoryPool<T>.SharedAllocatingPool.Rent((uint)span.Length)), span);

        internal static OverwritableNativeQueue<T> CreateOverwritableNativeQueue<T>(ReadOnlySpan<T> span) => new(CreateCore(span));

        internal static BlockingNativePile<T> CreateNativePile<T>(ReadOnlySpan<T> span) => new(span);
    }
}
