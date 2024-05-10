using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Threading;

namespace ModernMemory.Collections
{
    public sealed partial class BatchingBuffer<T>(NativeMemoryPool<T> pool) : IDisposable, IReadOnlyNativeList<T>, ISpanEnumerable<T>
    {
        private NativeQueue<T> primaryAppendQueue = new(pool);
        private NativeQueue<T> secondaryAppendQueue = new(pool);
        private NativeQueue<T> readQueue = new(pool);
        private ValueSpinLockSlim swapLock = new();
        private uint disposedValue = AtomicUtils.GetValue(false);

        public nuint Count => readQueue.Count;

        public T this[nuint index] => readQueue[index];

        public BatchingBuffer() : this(NativeMemoryPool<T>.Shared) { }

        public void Add(T item) => Add([item]);

        public void Add(ReadOnlyNativeSpan<T> items)
        {
            using var al = swapLock.Enter();
            primaryAppendQueue.Add(items);
        }

        public void Swap()
        {
            using (var al = swapLock.Enter())
            {
                (secondaryAppendQueue, primaryAppendQueue) = (primaryAppendQueue, secondaryAppendQueue);
            }
            secondaryAppendQueue.DequeueAll(ref readQueue);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => ((IEnumerable<T>)readQueue).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)readQueue).GetEnumerator();
        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => readQueue.GetEnumerator();
    }

    public sealed partial class BatchingBuffer<T>
    {
        private void Dispose(bool disposing)
        {
            if (!AtomicUtils.Exchange(ref disposedValue, true))
            {
                if (disposing && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    primaryAppendQueue.Clear();
                    secondaryAppendQueue.Clear();
                    readQueue.Clear();
                }
                primaryAppendQueue.Dispose();
                secondaryAppendQueue.Dispose();
                readQueue.Dispose();
            }
        }

        // // TODO: override finalizer only if 'DisposeCore(bool disposing)' has code to free unmanaged resources
        // ~BatchingBuffer()
        // {
        //     // Do not change this code. Put cleanup code in 'DisposeCore(bool disposing)' method
        //     DisposeCore(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'DisposeCore(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
