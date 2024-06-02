using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.DataFlow;
using ModernMemory.Threading;

namespace ModernMemory.Collections.Concurrent
{
    public sealed partial class BlockingNativeQueue<T> : IDisposable, IReadOnlyNativeList<T>, ISpanEnumerable<T>, IQueue<T>
    {
        private DisposableValueSpinLockSlim mutateLock = new();
        private NativeQueueCore<T> core;

        public T this[nuint index] => core[index];

        public nuint Count => core.Count;

        public ReadOnlyNativeSpan<T> Span => core.Span;

        public ReadOnlyNativeMemory<T> Memory => core.Memory;

        public BlockingNativeQueue()
        {
            core = new(new MemoryResizer<T>());
        }

        internal BlockingNativeQueue(NativeQueueCore<T> core)
        {
            this.core = core;
        }

        public BlockingNativeQueue(NativeMemoryPool<T> pool)
        {
            core = new(pool);
        }

        public BlockingNativeQueue(nuint initialSize)
        {
            core = new(initialSize);
        }
        public BlockingNativeQueue(NativeMemoryPool<T> pool, nuint initialSize)
        {
            core = new(pool, initialSize);
        }

        public void Add(ReadOnlyNativeSpan<T> items)
        {
            using var k = mutateLock.Enter();
            core.Add(items);
        }

        public void Add(T item)
        {
            using var k = mutateLock.Enter();
            core.Add(item);
        }

        public void Clear()
        {
            using var k = mutateLock.Enter();
            core.Clear();
        }

        public T? Dequeue()
        {
            using var k = mutateLock.Enter();
            return core.Dequeue();
        }

        public void DequeueAll<TBufferWriter>(ref TBufferWriter writer) where TBufferWriter : IBufferWriter<T>
        {
            using var k = mutateLock.Enter();
            core.DequeueAll(ref writer);
        }

        public void DequeueRange<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
        {
            using var k = mutateLock.Enter();
            core.DequeueRange(ref writer);
        }

        public void DequeueRange<TBufferWriter>(ref TBufferWriter writer, nuint elements) where TBufferWriter : IBufferWriter<T>
        {
            using var k = mutateLock.Enter();
            core.DequeueRange(ref writer, elements);
        }

        public void DequeueRangeExact(NativeSpan<T> destination)
        {
            using var k = mutateLock.Enter();
            core.DequeueRangeExact(destination);
        }
        public nuint DequeueRangeAtMost(NativeSpan<T> destination)
        {
            using var k = mutateLock.Enter();
            return core.DequeueRangeAtMost(destination);
        }

        public nuint DequeueRangeAtMost<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
        {
            using var k = mutateLock.Enter();
            return core.DequeueRangeAtMost(ref writer);
        }

        public void DiscardHead(nuint count)
        {
            using var k = mutateLock.Enter();
            core.DiscardHead(count);
        }
        public nuint DiscardHeadAtMost(nuint count)
        {
            using var k = mutateLock.Enter();
            return core.DiscardHeadAtMost(count);
        }
        public void EnsureCapacityToAdd(nuint size)
        {
            using var k = mutateLock.Enter();
            core.EnsureCapacityToAdd(size);
        }
        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => core.GetEnumerator();
        public T? Peek() => core.Peek();
        public bool TryDequeue(out T? item)
        {
            using var k = mutateLock.Enter();
            return core.TryDequeue(out item);
        }

        public bool TryPeek(out T? item) => core.TryPeek(out item);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => core.GetCopiedValuesEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => core.GetCopiedValuesEnumerator();
    }

    public sealed partial class BlockingNativeQueue<T>
    {
        private void Dispose(bool disposing)
        {
            var k = mutateLock.Enter(out _);
            core.DisposeCore(disposing);
            k.ExitAndDispose();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
