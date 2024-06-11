using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections.Concurrent
{
    public sealed partial class BoundedNativeRingQueue<T>
    {
        public readonly ref struct Writer
        {
            readonly BoundedNativeRingQueue<T> queue;
            readonly NativeSpan<T> span;

            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public Writer(BoundedNativeRingQueue<T> queue) : this()
            {
                this.queue = queue ?? throw new ArgumentNullException(nameof(queue));
                span = queue.memory.Span;
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
        }
    }
}
