using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Buffers.Pooling;
using ModernMemory.Collections.Storage;
using ModernMemory.DataFlow;
using ModernMemory.Threading;

namespace ModernMemory.Collections.Concurrent
{
    /// <summary>
    /// A lock-free single-reader single-writer thread-safe queue.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    [CollectionBuilder(typeof(NativeCollectionBuilder), nameof(NativeCollectionBuilder.CreateBoundedNativeRingQueue2))]
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    public sealed partial class BoundedNativeRingQueue2<T> : IDisposable, IQueue<T>, IEnumerable<T>
    {
        private BoundedNativeRingQueueCore<T, MemoryOwnerContainerStorage<T>> core;

        public BoundedNativeRingQueue2(MemoryOwnerContainerStorage<T> storage)
        {
            core = new(storage);
        }

        public BoundedNativeRingQueue2(nuint capacity)
        {
            core = new(new(NativeMemoryPool<T>.SharedAllocatingPool.Rent(capacity)));
        }

        public BoundedNativeRingQueue2(MemoryOwnerContainerStorage<T> storage, ReadOnlyNativeSpan<T> initialValues)
        {
            core = new(storage, initialValues);
        }

        public nuint Capacity => core.Capacity;

        public nuint Count => core.Count;

        public T this[nuint index] => core[index];

        public Writer GetWriter() => new(ref core);

        public void Clear() => core.Clear();

        [SkipLocalsInit]
        public bool TryAdd(T item) => core.TryAdd(item);

        [SkipLocalsInit]
        public nuint AddAtMost(ReadOnlyNativeSpan<T> items) => core.AddAtMost(items);

        public void Add(T item)
        {
            if (!TryAdd(item))
            {
                throw new InvalidOperationException("Tried to add an item to the queue while it's full!");
            }
        }

        public void Add(ReadOnlyNativeSpan<T> items) => core.Add(items);

        public ref T? TryPeekRef(out bool success) => ref core.TryPeekRef(out success);

        public bool TryPeek(out T? item) => core.TryPeek(out item);

        public T? Peek() => core.Peek();

        public nuint PeekRangeAtMost(NativeSpan<T> destination) => core.PeekRangeAtMost(destination);

        public bool TryDequeue(out T? item) => core.TryDequeue(out item);

        public T? Dequeue() => core.Dequeue();

        public void DequeueAll<TBufferWriter>(ref TBufferWriter writer) where TBufferWriter : IBufferWriter<T>
            => core.DequeueAll(ref writer);
        public void DequeueRange<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
            => core.DequeueRange(ref writer);
        public void DequeueRange<TBufferWriter>(ref TBufferWriter writer, nuint elements) where TBufferWriter : IBufferWriter<T>
            => core.DequeueRange(ref writer, elements);
        public void DequeueRangeExact(NativeSpan<T> destination) => core.DequeueRangeExact(destination);
        public nuint DequeueRangeAtMost<TBufferWriter>(scoped ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
            => core.DequeueRangeAtMost(ref writer);

        public nuint DequeueRangeAtMost(NativeSpan<T> destination) => core.DequeueRangeAtMost(destination);

        public IEnumerator<T> GetEnumerator() => core.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)core).GetEnumerator();
        public void DiscardHead(nuint count) => core.DiscardHead(count);
        public nuint DiscardHeadAtMost(nuint count) => core.DiscardHeadAtMost(count);
    }

    public sealed partial class BoundedNativeRingQueue2<T> : IDisposable
    {
        private void Dispose(bool disposing)
        {
            _ = core.DisposeCore(disposing);
            core = default;
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

}
