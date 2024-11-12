﻿using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.DataFlow;
using ModernMemory.Threading;

namespace ModernMemory.Collections
{
    [CollectionBuilder(typeof(NativeCollectionBuilder), nameof(NativeCollectionBuilder.CreateNativeQueue))]
    public sealed partial class NativeQueue<T> : IDisposable, IReadOnlyNativeList<T>, INativeBufferWriter<T>, ISpanEnumerable<T>, IQueue<T>
    {
        private NativeQueueCore<T> core;

        public T this[nuint index] => core[index];

        public nuint Count => core.Count;

        public ReadOnlyNativeSpan<T> Span => core.Span;

        public ReadOnlyNativeMemory<T> Memory => core.Memory;

        public NativeQueue()
        {
            core = new(new MemoryResizer<T>());
        }

        internal NativeQueue(NativeQueueCore<T> core)
        {
            this.core = core;
        }

        public NativeQueue(NativeMemoryPool<T> pool)
        {
            core = new(pool);
        }

        public NativeQueue(nuint initialSize)
        {
            core = new(initialSize);
        }
        public NativeQueue(NativeMemoryPool<T> pool, nuint initialSize)
        {
            core = new(pool, initialSize);
        }

        public void Add(ReadOnlyNativeSpan<T> items) => core.Add(items);
        public void Add(T item) => core.Add(item);
        public void Add(ReadOnlySequenceSlim<T> items) => core.Add(items);
        public void Advance(nuint count) => core.Advance(count);
        public void Clear() => core.Clear();
        public T? Dequeue() => core.Dequeue();
        public void DequeueAll<TBufferWriter>(ref TBufferWriter writer) where TBufferWriter : IBufferWriter<T> => core.DequeueAll(ref writer);
        public void DequeueRange<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T> => core.DequeueRange(ref writer);
        public void DequeueRange<TBufferWriter>(ref TBufferWriter writer, nuint elements) where TBufferWriter : IBufferWriter<T> => core.DequeueRange(ref writer, elements);
        public void DequeueRangeExact(NativeSpan<T> destination) => core.DequeueRangeExact(destination);
        public nuint DequeueRangeAtMost(NativeSpan<T> destination) => core.DequeueRangeAtMost(destination);
        public nuint DequeueRangeAtMost<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T> => core.DequeueRangeAtMost(ref writer);
        public void DiscardHead(nuint count) => core.DiscardHead(count);
        public nuint DiscardHeadAtMost(nuint count) => core.DiscardHeadAtMost(count);
        public void EnsureCapacityToAdd(nuint size) => core.EnsureCapacityToAdd(size);
        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => core.GetEnumerator();
        public Memory<T> GetMemory(int sizeHint = 0) => core.GetMemory(sizeHint);
        public NativeMemory<T> GetNativeMemory(nuint sizeHint = 0U) => core.GetNativeMemory(sizeHint);
        public NativeSpan<T> GetNativeSpan(nuint sizeHint = 0U) => core.GetNativeSpan(sizeHint);
        public Span<T> GetSpan(int sizeHint = 0) => core.GetSpan(sizeHint);
        public T? Peek() => core.Peek();
        public bool TryDequeue(out T? item) => core.TryDequeue(out item);
        public bool TryGetMaxBufferSize(out nuint space) => core.TryGetMaxBufferSize(out space);
        public NativeMemory<T> TryGetNativeMemory(nuint sizeHint = 0U) => core.TryGetNativeMemory(sizeHint);
        public NativeSpan<T> TryGetNativeSpan(nuint sizeHint = 0U) => core.TryGetNativeSpan(sizeHint);
        public bool TryPeek(out T? item) => core.TryPeek(out item);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => core.GetCopiedValuesEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => core.GetCopiedValuesEnumerator();
    }

    public sealed partial class NativeQueue<T>
    {
        private void Dispose(bool disposing) => core.DisposeCore(disposing);

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
