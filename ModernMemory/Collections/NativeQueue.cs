using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Buffers.DataFlow;
using ModernMemory.Threading;

namespace ModernMemory.Collections
{
    public sealed partial class NativeQueue<T> : IDisposable, IReadOnlyNativeList<T>, INativeBufferWriter<T>, ISpanEnumerable<T>
    {
        private uint disposedValue = AtomicUtils.GetValue(false);
        private nuint readHead;
        private nuint count;
        private MemoryResizer<T> resizer;

        public NativeQueue() : this(new MemoryResizer<T>()) { }

        public NativeQueue(NativeMemoryPool<T> pool) : this(new MemoryResizer<T>(pool)) { }

        public NativeQueue(nuint initialSize) : this(new MemoryResizer<T>(initialSize)) { }
        public NativeQueue(NativeMemoryPool<T> pool, nuint initialSize) : this(new MemoryResizer<T>(pool, initialSize)) { }

        private NativeQueue(MemoryResizer<T> resizer)
        {
            this.resizer = resizer;
            readHead = 0;
            count = 0;
        }

        private NativeMemory<T> NativeMemory => resizer.NativeMemory;

        private NativeSpan<T> VisibleValues => NativeMemory.Span.Slice(readHead, count);

        private NativeSpan<T> Writable => NativeMemory.Span.Slice(readHead + count);

        public nuint Count => count;

        public T this[nuint index] => VisibleValues[index];

        public ReadOnlyNativeSpan<T> Span => VisibleValues;

        public ReadOnlyNativeMemory<T> Memory => NativeMemory.Slice(readHead, count);

        public void Add(T item) => Add([item]);

        public void Add(ReadOnlyNativeSpan<T> items)
        {
            EnsureCapacityToAdd(items.Length);
            var m = items.CopyAtMostTo(Writable);
            count += m;
            Debug.Assert(m == items.Length);
        }

        public T Peek()
        {
            var m = VisibleValues;
            return m.IsEmpty ? throw new InvalidOperationException("The Queue is empty!") : m.Head;
        }

        public T Dequeue()
        {
            var m = VisibleValues;
            if (m.IsEmpty) throw new InvalidOperationException("The Queue is empty!");
            ref var rr = ref m.Head;
            var res = rr;
            rr = default;
            count--;
            readHead++;
            return res;
        }

        public nuint DequeueRange(NativeSpan<T> destination)
        {
            var length = destination.Length;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, count);
            var span = NativeMemory.Span.Slice(readHead, length);
            length = span.CopyAtMostTo(destination);
            count -= length;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                span.SliceWhileIfLongerThan(length).Clear();
            }
            readHead += length;
            return length;
        }

        public void DequeueRange<TBufferWriter>(scoped ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
        {
            if (writer.IsCompleted) return;
            var d = writer.WriteAtMost(VisibleValues);
            if (!writer.IsCompleted) throw new InvalidOperationException("Not enough data to write!");
            DiscardHead(d);
        }

        public void DequeueRange<TBufferWriter>(scoped ref TBufferWriter writer, nuint elements) where TBufferWriter : IBufferWriter<T>
        {
            var dw = DataWriter<T>.CreateFrom(ref writer, elements);
            DequeueRange(ref dw);
            dw.Dispose();
        }

        public nuint DequeueRangeAtMost<TBufferWriter>(scoped ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
        {
            if (writer.IsCompleted || count == 0) return 0;
            var d = writer.WriteAtMost(VisibleValues);
            DiscardHead(d);
            return d;
        }

        public void DequeueAll<TBufferWriter>(scoped ref TBufferWriter writer) where TBufferWriter : IBufferWriter<T>
        {
            var dw = DataWriter<T>.CreateFrom(ref writer);
            DequeueRange(ref dw);
            dw.Dispose();
        }

        public void DiscardHead(nuint count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, this.count);
            this.count -= count;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                NativeMemory.Span.Slice(readHead, count).Clear();
            }
            readHead += count;
        }

        private void TrimHead()
        {
            var m = NativeMemory;
            var c = count;
            var rH = readHead;
            readHead = 0;
            if (c == 0 || rH == m.Length)
            {
                count = 0;
                return;
            }
            var span = m.Span;
            var v = span.Slice(rH, c);
            v.CopyTo(span);
        }

        public void EnsureCapacityToAdd(nuint size)
        {
            if (Writable.Length >= size)
            {
                return;
            }
            TrimHead();
            if (Writable.Length < size)
            {
                resizer.Resize(count + size);
            }
            Debug.Assert(Writable.Length >= size);
        }

        public void Clear()
        {
            readHead = 0;
            count = 0;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                NativeMemory.Span.Clear();
            }
        }

        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => new(VisibleValues);
        public bool TryGetMaxBufferSize(out nuint space)
        {
            space = nuint.MaxValue;
            return false;
        }
        public void Advance(nuint count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, Writable.Length);
            this.count += count;
        }
        public NativeSpan<T> GetNativeSpan(nuint sizeHint = 0U)
        {
            EnsureCapacityToAdd(sizeHint);
            return Writable;
        }
        public NativeMemory<T> GetNativeMemory(nuint sizeHint = 0U)
        {
            EnsureCapacityToAdd(sizeHint);
            return NativeMemory.Slice(readHead + count);
        }
        public NativeSpan<T> TryGetNativeSpan(nuint sizeHint = 0U) => sizeHint > ~(readHead + count) ? Writable : GetNativeSpan(sizeHint);
        public NativeMemory<T> TryGetNativeMemory(nuint sizeHint = 0U) => sizeHint > ~(readHead + count) ? NativeMemory.Slice(readHead + count) : GetNativeMemory(sizeHint);
        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            Advance((nuint)count);
        }
        public Memory<T> GetMemory(int sizeHint = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            return GetNativeMemory((nuint)sizeHint).GetHeadMemory();
        }
        public Span<T> GetSpan(int sizeHint = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            return GetNativeSpan((nuint)sizeHint).GetHeadSpan();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => throw new NotImplementedException();
        IEnumerator IEnumerable.GetEnumerator() => throw new NotImplementedException();
    }

    public sealed partial class NativeQueue<T>
    {
        private void Dispose(bool disposing)
        {
            if (!AtomicUtils.Exchange(ref disposedValue, true))
            {
                if (disposing)
                {
                    resizer.Dispose();
                }
                resizer = default;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
