using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.DataFlow;

namespace ModernMemory.Collections
{

    /// <summary>
    /// The building block for various queue collection types.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [CollectionBuilder(typeof(NativeCollectionBuilder), nameof(NativeCollectionBuilder.CreateCore))]
    internal partial struct NativeQueueCore<T> : INativeList<T>, INativeBufferWriter<T>, ISpanEnumerable<T>, IQueue<T>
    {
        private uint disposedValue = AtomicUtils.GetValue(false);
        private nuint readHead;
        private nuint writeHead;
        internal MemoryResizer<T> resizer;

        internal readonly bool IsDisposed => AtomicUtils.GetValue(disposedValue);

        internal readonly NativeMemory<T> NativeMemory => resizer.Memory;

        internal readonly NativeSpan<T> VisibleValues => NativeMemory.Span.Slice(readHead, writeHead - readHead);

        internal readonly NativeSpan<T> Writable => NativeMemory.Span.Slice(writeHead);

        public readonly nuint Count => VisibleValues.Length;

        public readonly NativeMemory<T> Memory => NativeMemory.SliceByRange(readHead, writeHead);

        public readonly NativeSpan<T> Span => VisibleValues;

        public readonly T this[nuint index] { get => VisibleValues[index]; set => VisibleValues[index] = value; }

        internal NativeQueueCore(MemoryResizer<T> resizer)
        {
            this.resizer = resizer;
            readHead = 0;
            writeHead = 0;
        }

        public NativeQueueCore() : this(new MemoryResizer<T>()) { }

        public NativeQueueCore(ReadOnlyNativeSpan<T> values) : this(values.Length)
        {
            Add(values);
        }

        public NativeQueueCore(NativeMemoryPool<T> pool) : this(new MemoryResizer<T>(pool)) { }

        public NativeQueueCore(nuint initialSize) : this(new MemoryResizer<T>(initialSize)) { }
        public NativeQueueCore(NativeMemoryPool<T> pool, nuint initialSize) : this(new MemoryResizer<T>(pool, initialSize)) { }
        private void ExpandIfNeeded(nuint size)
        {
            LazyTrimHead(size);
            Debug.Assert(Writable.Length >= size);
        }

        private void LazyTrimHead(nuint addingElements)
        {
            var m = NativeMemory;
            var rH = readHead;
            var wH = writeHead;
            var c = checked(wH - rH);
            readHead = 0;
            if (c == 0 || rH == m.Length)
            {
                c = 0;
                rH = 0;
            }
            var span = m.Span;
            var v = span.Slice(rH, c);
            var newSize = c + addingElements;
            resizer.Resize(newSize, v);
            writeHead = c;
        }

        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static T2 ThrowInsufficientItemsException<T2>(nuint count, nuint demand = 0)
        {
            if (count == 0)
            {
                throw new InvalidOperationException("The Queue is empty!");
            }
            throw new InvalidOperationException($"The Queue has only {count} items, while {demand} element(s) are needed!");
        }

        internal void TrimHead() => LazyTrimHead(0);

        public void Add(T item) => Add([item]);

        public void Add(ReadOnlyNativeSpan<T> items)
        {
            EnsureCapacityToAdd(items.Length);
            var m = items.CopyAtMostTo(Writable);
            writeHead += m;
            Debug.Assert(m == items.Length);
        }

        public void Add(ReadOnlySequenceSlim<T> items)
        {
            EnsureCapacityToAdd(items.Length);
            foreach (var segment in items.GetSegmentsEnumerable())
            {
                Add(segment.Span);
            }
        }

        public void Advance(nuint count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Writable.Length);
            writeHead += count;
        }

        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            Advance((nuint)count);
        }

        public void Clear()
        {
            var rH = readHead;
            var wH = writeHead;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                var c = checked(wH - rH);
                if (c > 0)
                {
                    NativeMemory.Span.Clear();
                }
            }
            writeHead = 0;
            readHead = 0;
        }

        public T? Dequeue() => TryDequeue(out var item) ? item : ThrowInsufficientItemsException<T>(0);

        public void DequeueAll<TBufferWriter>(scoped ref TBufferWriter writer) where TBufferWriter : IBufferWriter<T>
        {
            var dw = DataWriter<T>.CreateFrom(ref writer);
            DequeueRangeAtMost(ref dw);
            dw.Dispose();
            if (Count > 0) throw new ArgumentException("The writer ran out of space writing all elements in the appendQueue!", nameof(writer));
        }

        public void DequeueRange<TBufferWriter>(scoped ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
        {
            if (writer.IsCompleted) return;
            var d = writer.WriteAtMost(VisibleValues);
            if (writer.IsLengthConstrained && !writer.IsCompleted) throw new InvalidOperationException("Not enough data to write!");
            DiscardHead(d);
        }

        public void DequeueRange<TBufferWriter>(scoped ref TBufferWriter writer, nuint elements) where TBufferWriter : IBufferWriter<T>
        {
            var dw = DataWriter<T>.CreateFrom(ref writer, elements);
            DequeueRange(ref dw);
            dw.Dispose();
        }

        public void DequeueRangeExact(NativeSpan<T> destination)
        {
            var length = destination.Length;
            var span = VisibleValues;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, span.Length);
            span = span.Slice(0, length);
            length = span.CopyAtMostTo(destination);
            readHead += length;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                span.SliceWhileIfLongerThan(length).Clear();
            }
        }

        public nuint DequeueRangeAtMost<TBufferWriter>(scoped ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
        {
            if (writer.IsCompleted || Count == 0) return 0;
            var d = writer.WriteAtMost(VisibleValues);
            DiscardHead(d);
            return d;
        }

        public nuint DequeueRangeAtMost(NativeSpan<T> destination)
        {
            var span = VisibleValues;
            var length = span.CopyAtMostTo(destination);
            readHead += length;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                span.SliceWhileIfLongerThan(length).Clear();
            }
            return length;
        }

        public void DiscardHead(nuint count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Count);
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                VisibleValues.Slice(0, count).Clear();
            }
            readHead += count;
        }

        public nuint DiscardHeadAtMost(nuint count)
        {
            count = nuint.Min(count, Count);
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                VisibleValues.Slice(0, count).Clear();
            }
            readHead += count;
            return count;
        }

        public void EnsureCapacityToAdd(nuint size)
        {
            if (Writable.Length >= size)
            {
                return;
            }
            ExpandIfNeeded(size);
        }

        public readonly ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => new(VisibleValues);

        readonly IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetCopiedValuesEnumerator();
        internal readonly CopiedValuesEnumerator<T> GetCopiedValuesEnumerator() => new(Span);
        readonly IEnumerator IEnumerable.GetEnumerator() => GetCopiedValuesEnumerator();

        public Memory<T> GetMemory(int sizeHint = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            return GetNativeMemory((nuint)sizeHint).GetHeadMemory();
        }

        public NativeMemory<T> GetNativeMemory(nuint sizeHint = 0U)
        {
            EnsureCapacityToAdd(sizeHint);
            return NativeMemory.Slice(writeHead);
        }

        public NativeSpan<T> GetNativeSpan(nuint sizeHint = 0U)
        {
            EnsureCapacityToAdd(sizeHint);
            return Writable;
        }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            return GetNativeSpan((nuint)sizeHint).GetHeadSpan();
        }

        public readonly T? Peek() => TryPeek(out var item) ? item : throw new InvalidOperationException("The Queue is empty!");

        public bool TryDequeue(out T? item)
        {
            Unsafe.SkipInit(out item);
            var m = VisibleValues;
            if (!m.IsEmpty)
            {
                ref var rr = ref m.Head;
                (item, rr) = (rr, default!);
                readHead++;
                return true;
            }
            return false;
        }

        public readonly bool TryGetMaxBufferSize(out nuint space)
        {
            space = nuint.MaxValue;
            return false;
        }

        public NativeMemory<T> TryGetNativeMemory(nuint sizeHint = 0U) => sizeHint > ~writeHead ? NativeMemory.Slice(writeHead) : GetNativeMemory(sizeHint);

        public NativeSpan<T> TryGetNativeSpan(nuint sizeHint = 0U) => sizeHint > ~writeHead ? Writable : GetNativeSpan(sizeHint);

        public readonly bool TryPeek(out T? item)
        {
            Unsafe.SkipInit(out item);
            var m = VisibleValues;
            if (!m.IsEmpty) item = m.Head;
            return !m.IsEmpty;
        }
    }

    internal partial struct NativeQueueCore<T>
    {
        internal bool DisposeCore(bool disposing)
        {
            var res = !AtomicUtils.Exchange(ref disposedValue, true);
            if (res)
            {
                if (disposing)
                {
                    resizer.Dispose();
                }
                resizer = default;
            }
            return res;
        }
    }
}
