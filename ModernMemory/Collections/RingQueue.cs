using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public sealed class RingQueue<T>(int capacity = 64) : IReadOnlyList<T>, IBufferWriter<T>
    {
        private int readHead = 0;
        private int writeHead = 0;
        private T[] values = new T[capacity];
        private volatile uint version = 0;

        public T this[int index]
        {
            get
            {
                var i = GetIndex(readHead + index, values.Length);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(i, writeHead);
                return values[i];
            }
        }

        public bool IsUninitialized => values is null || values.Length == 0 || (uint)values.Length <= uint.Max((uint)readHead, (uint)writeHead);

        public bool IsEmpty => readHead == writeHead;

        public T? Peek() => values[readHead];

        public int Peek(Span<T> destination)
        {
            var r = readHead;
            var w = writeHead;
            var vs = values.AsSpan();
            if (w == r) return 0;
            var sh = vs.Slice(r);
            var c = w - r;
            if (c < 0) c = sh.Length;
            if (destination.Length < c) c = destination.Length;
            sh.Slice(0, c).CopyTo(destination);
            if (w < r && sh.Length < destination.Length)
            {
                var d2 = destination.Slice(sh.Length);
                var sl = vs.Slice(0, int.Min(w, d2.Length));
                sl.CopyTo(d2);
                c += sl.Length;
            }
            return c;
        }

        public int PeekWithOffset(Span<T> destination, int offset)
        {
            var r = readHead;
            var w = writeHead;
            var vs = values.AsSpan();
            if (w == r || Count <= offset) return 0;
            r = GetIndex(r + offset, vs.Length);
            var sh = vs.Slice(r);
            var c = w - r;
            if (c < 0) c = sh.Length;
            if (destination.Length < c) c = destination.Length;
            sh.Slice(0, c).CopyTo(destination);
            if (w < r && sh.Length < destination.Length)
            {
                var d2 = destination.Slice(sh.Length);
                var sl = vs.Slice(0, int.Min(w, d2.Length));
                sl.CopyTo(d2);
                c += sl.Length;
            }
            return c;
        }

        public T? Dequeue()
        {
            var r = readHead;
            if (r != writeHead)
            {
                version++;
                readHead = GetIndex(r + 1, values.Length);
                ref var t = ref values[r];
                var res = t;
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) t = default;
                return res;
            }
            throw new InvalidOperationException("The RingQueue is empty!");
        }

        public bool TryDequeue(out T value)
        {
            var r = readHead;
            ref var t = ref values[r];
            value = t;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) t = default;
            var v = r != writeHead;
            readHead = GetIndex(r + (v ? 1 : 0), values.Length);
            version += v ? 1u : 0u;
            return v;
        }

        public int DequeueRange(Span<T> destination)
        {
            var r = readHead;
            var w = writeHead;
            var vs = values.AsSpan();
            if (w == r || destination.IsEmpty) return 0;
            version++;
            var sh = vs.Slice(r);
            var c = w - r;
            if (c < 0) c = sh.Length;
            if (destination.Length < c) c = destination.Length;
            var read = sh.Slice(0, c);
            read.CopyTo(destination);
            read.ClearIfReferenceOrContainsReferences();
            if (w < r && sh.Length < destination.Length)
            {
                var d2 = destination.Slice(sh.Length);
                var sl = vs.Slice(0, int.Min(w, d2.Length));
                sl.CopyTo(d2);
                sl.ClearIfReferenceOrContainsReferences();
                c += sl.Length;
            }
            readHead = GetIndex(r + c, values.Length);
            if (readHead == w)
            {
                readHead = writeHead = 0;
            }
            return c;
        }

        public void DequeueRange<TBufferWriter>(TBufferWriter bufferWriter, int elements) where TBufferWriter : IBufferWriter<T>
        {
            var r = readHead;
            var w = writeHead;
            var vs = values.AsSpan();
            if (w == r || elements <= 0) return;
            version++;
            var sh = vs.Slice(r);
            var c = w - r;
            if (c < 0) c = sh.Length;
            if (elements < c) c = elements;
            var read = sh.Slice(0, c);
            bufferWriter.Write(read);
            read.ClearIfReferenceOrContainsReferences();
            if (w < r && sh.Length < elements)
            {
                var d2l = elements - sh.Length;
                var sl = vs.Slice(0, int.Min(w, d2l));
                bufferWriter.Write(sl);
                sl.ClearIfReferenceOrContainsReferences();
                c += sl.Length;
            }
            readHead = GetIndex(r + c, values.Length);
            if (readHead == w)
            {
                readHead = writeHead = 0;
            }
        }

        public void DequeueAll<TBufferWriter>(TBufferWriter bufferWriter) where TBufferWriter : IBufferWriter<T>
        {
            var r = readHead;
            var w = writeHead;
            var vs = values.AsSpan();
            if (w == r) return;
            version++;
            var sh = vs.Slice(r);
            var c = w - r;
            if (c >= 0) sh = sh.Slice(0, c);
            bufferWriter.Write(sh);
            if (w < r)
            {
                var sl = vs.Slice(0, w);
                bufferWriter.Write(sl);
            }
            Clear();
        }

        public void ThrowValuesAway(int count)
        {
            var r = readHead;
            var w = writeHead;
            var vs = values.AsSpan();
            if (w == r || count == 0) return;
            version++;
            var sh = vs.Slice(r);
            var c = w - r;
            if (c < 0) c = sh.Length;
            var length = count;
            if (length < c) c = length;
            var read = sh.Slice(0, c);
            read.ClearIfReferenceOrContainsReferences();
            if (w < r && sh.Length < length)
            {
                var sl = vs.Slice(0, int.Min(w, count - sh.Length));
                sl.ClearIfReferenceOrContainsReferences();
                c += sl.Length;
            }
            readHead = GetIndex(r + c, values.Length);
        }

        public void Enqueue(T item)
        {
            var cap = EnsureCapacity(Count + 1);
            Debug.Assert(cap >= Count + 1);
            version++;
            var w = writeHead;
            values[w] = item;
            writeHead = GetIndex(w + 1, values.Length);
        }

        public void EnqueueRange(ReadOnlySpan<T> items)
        {
            var cap = EnsureCapacity(Count + items.Length);
            Debug.Assert(cap >= Count + 1);
            version += (items.Length > 0) ? 1u : 0u;
            var vs = values.AsSpan();
            var w = writeHead;
            var sh = vs.Slice(w);
            if (items.TryCopyTo(sh))
            {
                return;
            }
            var ih = items.Slice(0, sh.Length);
            ih.CopyTo(sh);
            var sl = vs.Slice(0, readHead);
            var il = items.Slice(sh.Length);
            il.CopyTo(sl);
        }

        public int EnsureCapacity(int requiredCapacity) => EnsureCapacity(requiredCapacity, false);

        private int EnsureCapacity(int requiredCapacity, bool bufferWriter)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(requiredCapacity);
            var cap = Capacity;
            if (bufferWriter)
            {
                cap = (int)uint.Min((uint)(cap - writeHead), (uint)(readHead - writeHead));
            }
            if (cap < requiredCapacity)
            {
                var newSize = (int)uint.Min((uint)Array.MaxLength, BitOperations.RoundUpToPowerOf2((uint)requiredCapacity));
                Resize(newSize);
            }
            return Capacity;
        }

        private void Resize(int newSize)
        {
            version++;
            var vs = values.AsSpan();
            if (newSize > values.Length)
            {
                var newArray = new T[newSize];
                writeHead = Peek(newArray);
                readHead = 0;
                vs.ClearIfReferenceOrContainsReferences();
                values = newArray;
            }
            else
            {
                var arr = ArrayPool<T>.Shared.Rent(vs.Length);
                var ns = arr.AsSpan(0, vs.Length);
                var wh = Peek(ns);
                writeHead = wh;
                readHead = 0;
                vs.ClearIfReferenceOrContainsReferences();
                ns.Slice(0, wh).CopyTo(vs);
                ArrayPool<T>.Shared.Return(arr);
            }
        }

        public void Clear()
        {
            version++;
            values.AsSpan().ClearIfReferenceOrContainsReferences();
            writeHead = 0;
            readHead = 0;
        }

        private static int GetIndex(int index, int capacity)
        {
            var i = (uint)index;
            var c = (uint)capacity;
            var j = i - c;
            var k = uint.Min(j, i);
            return k < c ? (int)k : (int)(i % c);
        }

        public int Count
        {
            get
            {
                var w = writeHead;
                var r = readHead;
                if (w < r) w += values.Length;
                return w - r;
            }
        }

        public int Capacity => values.Length;

        public IEnumerator<T> GetEnumerator()
        {
            var v = version;
            for (var i = 0; i < Count; i++)
            {
                yield return v == version ? this[i] : throw new InvalidOperationException("Enumerator version mismatch");
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, (int)uint.Min((uint)(Capacity - writeHead), (uint)(readHead - writeHead)));
            version++;
            writeHead += count;
        }
        public Memory<T> GetMemory(int sizeHint = 0)
        {
            if (sizeHint <= 0) sizeHint = (int)uint.Min((uint)(Capacity - writeHead), (uint)(readHead - writeHead));
            var cap = EnsureCapacity(Count + sizeHint, true);
            Debug.Assert(cap >= Count + sizeHint);
            version += (sizeHint > 0) ? 1u : 0u;
            var vs = values.AsMemory(writeHead);
            if (vs.Length <= readHead) vs.Slice(0, readHead - vs.Length);
            return vs;
        }
        public Span<T> GetSpan(int sizeHint = 0)
        {
            if (sizeHint <= 0) sizeHint = (int)uint.Min((uint)(Capacity - writeHead), (uint)(readHead - writeHead));
            var cap = EnsureCapacity(Count + sizeHint, true);
            Debug.Assert(cap >= Count + sizeHint);
            version += (sizeHint > 0) ? 1u : 0u;
            var vs = values.AsSpan(writeHead);
            if (vs.Length <= readHead) vs.Slice(0, readHead - vs.Length);
            return vs;
        }
    }
}
