using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Threading;

namespace ModernMemory.Collections.Concurrent
{
    public sealed partial class BoundedNativeRingQueue<T, TStorage>
    {
        public readonly ref struct Writer
        {
            readonly ref BoundedNativeRingQueueCore<T, TStorage> queue;
            readonly NativeSpan<T> span;

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            internal Writer(ref BoundedNativeRingQueueCore<T, TStorage> queue) : this()
            {
                this.queue = ref queue;
                span = queue.Span;
            }

            public bool TryAdd(T item) => queue.TryAddInternal(item, span);

            public nuint AddAtMost(ReadOnlyNativeSpan<T> items) => queue.AddAtMostInternal(items, span);

            public void Add(T item)
            {
                if (!TryAdd(item))
                {
                    throw new InvalidOperationException("Tried to add an item to the queue while it's full!");
                }
            }

            public void Add(ReadOnlyNativeSpan<T> items) => queue.AddInternal(items, span);

            public void WaitAdd(T item)
            {
                var localSpan = span;
                while (!queue.TryAddInternal(item, localSpan))
                {
                    ThreadingExtensions.Yield();
                }
            }
        }
    }
}
