﻿using System;
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
using ModernMemory.DataFlow;
using ModernMemory.Threading;

using static ModernMemory.Collections.Concurrent.BoundedNativeRingQueue;

namespace ModernMemory.Collections.Concurrent
{
    /// <summary>
    /// A lock-free single-reader single-writer thread-safe queue.
    /// </summary>
    /// <typeparam name="T">The type of items.</typeparam>
    [CollectionBuilder(typeof(NativeCollectionBuilder), nameof(NativeCollectionBuilder.CreateBoundedNativeRingQueue))]
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    public sealed partial class BoundedNativeRingQueue<T> : IDisposable, IQueue<T>, IEnumerable<T>
    {
        private nuint readCursorLowerBound;
        private PaddedUIntPtr writeCursorCache;
        private PaddedUIntPtr readCursor;
        private MemoryOwnerContainer<T> owner;
#pragma warning disable IDE0032 // Use auto property
        private NativeMemory<T> memory;
#pragma warning restore IDE0032 // Use auto property
        private uint disposedValue;
#pragma warning disable S1144 // Unused private types or members should be removed (needed for padding to prevent false sharing)
        private readonly Vector512<byte> padding;
#pragma warning restore S1144 // Unused private types or members should be removed
        private nuint writeCursorLowerBound;
        private PaddedUIntPtr writeCursor;
        private nuint readCursorCache;

        public BoundedNativeRingQueue(MemoryOwnerContainer<T> owner)
        {
            this.owner = owner;
            memory = owner.NativeMemory;
        }

        public BoundedNativeRingQueue(nuint capacity)
        {
            owner = NativeMemoryPool<T>.SharedAllocatingPool.Rent(capacity + 1);
            memory = owner.NativeMemory;
        }

        public BoundedNativeRingQueue(ReadOnlyNativeSpan<T> initialItems) : this(initialItems, NativeMemoryPool<T>.SharedAllocatingPool) { }

        public BoundedNativeRingQueue(ReadOnlyNativeSpan<T> initialItems, NativeMemoryPool<T> pool)
        {
            var capacity = BitOperations.RoundUpToPowerOf2(initialItems.Length);
            if (!initialItems.IsEmpty && capacity < initialItems.Length) capacity = initialItems.Length;
            owner = pool.Rent(capacity + 1);
            var span = owner.Span;
            var written = initialItems.CopyAtMostTo(span);
            memory = owner.NativeMemory;
            AdvanceWriteCursor(0, 0, span.Length, written);
        }

        public nuint Capacity => memory.Length - 1;

        public nuint Count => writeCursor.VolatileLoad() - readCursor.VolatileLoad();

        public T this[nuint index]
        {
            get
            {
                var rc = readCursor.Load();
                var rcl = readCursorLowerBound;
                var wc = writeCursorCache.Load();
                var m = memory;
                var pos = rc - rcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var readable = ReadableItemsWithCache(wc, rc, index + 1);
                if (readable > 0)
                {
                    var span = m.Span;
                    var readSpan0 = span.Slice(pos).SliceWhileIfLongerThan(readable);
                    var readSpan1 = span.SliceWhile(readable - readSpan0.Length);
                    return index < readSpan0.Length ? readSpan0[index] : readSpan1[index - readSpan0.Length];
                }
                throw new InvalidOperationException("Tried to read the queue while it's empty!");
            }
        }

        public void Clear()
        {
            var wc = writeCursor.VolatileLoad();
            var rc = readCursor.VolatileLoad();
            var rcl = readCursorLowerBound;
            AdvanceReadCursor(rc, ref rcl, memory.Length, wc - rc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceWriteCursor(nuint wc, nuint wcl, nuint capacity, nuint written)
        {
            wc += written;
            var ncl = wcl + capacity;
            writeCursor.VolatileSet(wc);
            if (wc >= ncl)
            {
                writeCursorLowerBound = ncl;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private nuint AdvanceReadCursor(nuint rc, ref nuint rcl, nuint capacity, nuint read)
        {
            rc += read;
            readCursor.VolatileSet(rc);
            var ncl = rcl + capacity;
            if (rc >= ncl)
            {
                rcl = ncl;
                readCursorLowerBound = ncl;
            }
            return rc;
        }

        [SkipLocalsInit]
        public bool TryAdd(T item)
        {
            var m = memory;
            var wc = writeCursor.Load();
            var rc = readCursorCache;
            var wcl = writeCursorLowerBound;
            var capacity = m.Length;
            var pos = wc - wcl;
            var s = m.Span;
            ref var dst = ref s[pos];
            var full = IsFullWithCache(wc, rc, capacity);
            if (!full)
            {
                dst = item;
                AdvanceWriteCursor(wc, wcl, capacity, 1);
                return true;
            }
            return false;
        }

        public nuint AddAtMost(ReadOnlyNativeSpan<T> items)
        {
            var wc = writeCursor.Load();
            var wcl = writeCursorLowerBound;
            nuint written = 0;
            if (!items.IsEmpty)
            {
                var rc = readCursorCache;
                var m = memory;
                var pos = wc - wcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var writableCount = WritableItemsWithCache(wc, rc, capacity, items.Length);
                if (writableCount > 0)
                {
                    var span = m.Span;
                    var writeSpan0 = span.Slice(pos).SliceWhileIfLongerThan(writableCount);
                    written = items.CopyAtMostTo(writeSpan0);
                    if (written < writableCount && written < items.Length)
                    {
                        writableCount -= written;
                        var writeSpan1 = span.SliceWhile(writableCount);
                        written += items.Slice(written).CopyAtMostTo(writeSpan1);
                    }
                    AdvanceWriteCursor(wc, wcl, capacity, written);
                }
            }
            return written;
        }

        public void Add(T item)
        {
            if (!TryAdd(item))
            {
                throw new InvalidOperationException("Tried to add an item to the queue while it's full!");
            }
        }
        public void Add(ReadOnlyNativeSpan<T> items)
        {
            var wc = writeCursor.Load();
            var wcl = writeCursorLowerBound;
            if (!items.IsEmpty)
            {
                var rc = readCursorCache;
                var m = memory;
                var pos = wc - wcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var writableCount = WritableItemsWithCache(wc, rc, capacity, items.Length);
                if (writableCount >= items.Length)
                {
                    var span = m.Span;
                    var writeSpan0 = span.Slice(pos).SliceWhileIfLongerThan(writableCount);
                    var written = items.CopyAtMostTo(writeSpan0);
                    if (written < writableCount && written < items.Length)
                    {
                        writableCount -= written;
                        var writeSpan1 = span.SliceWhile(writableCount);
                        written += items.Slice(written).CopyAtMostTo(writeSpan1);
                    }
                    AdvanceWriteCursor(wc, wcl, capacity, written);
                }
                else
                {
                    throw new InvalidOperationException($"Tried to add {items.Length} items to the queue while it cannot accept all of them!");
                }
            }
        }

        public ref T? TryPeekRef(out bool success)
        {
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound;
            var wc = writeCursorCache.Load();
            var m = memory;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var empty = IsEmptyWithCache(wc, rc);
            success = !empty;
            ref var src = ref Unsafe.NullRef<T>();
            if (!empty) src = ref m.Span[pos]!;
            return ref src;
        }

        public bool TryPeek(out T? item)
        {
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound;
            var wc = writeCursorCache.Load();
            var m = memory;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var empty = IsEmptyWithCache(wc, rc);
            item = !empty ? m.Span[pos] : default;
            return !empty;
        }

        public T? Peek() => TryPeek(out var item) ? item : throw new InvalidOperationException("Tried to peek the queue while it's empty!");

        public nuint PeekRangeAtMost(NativeSpan<T> destination)
        {
            nuint read = 0;
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound;
            if (!destination.IsEmpty)
            {
                var wc = writeCursorCache.Load();
                var m = memory;
                var pos = rc - rcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var readable = ReadableItemsWithCache(wc, rc, destination.Length);
                if (readable > 0)
                {
                    var span = m.Span;
                    var readSpan0 = span.Slice(pos).SliceWhileIfLongerThan(readable);
                    read = readSpan0.CopyAtMostTo(destination);
                    if (read < readable && read < destination.Length)
                    {
                        readable -= read;
                        var readSpan1 = span.SliceWhile(readable);
                        read += readSpan1.CopyAtMostTo(destination.Slice(read));
                    }
                }
            }
            return read;
        }

        public bool TryDequeue(out T? item)
        {
            Unsafe.SkipInit(out item);
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound;
            var wc = writeCursorCache.Load();
            var m = memory;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var empty = IsEmptyWithCache(wc, rc);
            if (!empty)
            {
                item = m.Span[pos];
                AdvanceReadCursor(rc, ref rcl, capacity, 1);
            }
            return !empty;
        }

        public T? Dequeue() => TryDequeue(out var item) ? item : throw new InvalidOperationException("Tried to dequeue an item from the queue while it's empty!");
        public void DequeueAll<TBufferWriter>(ref TBufferWriter writer) where TBufferWriter : IBufferWriter<T>
        {
            var dw = DataWriter<T>.CreateFrom(ref writer);
            DequeueRangeAtMost(ref dw);
            dw.Dispose();
        }
        public void DequeueRange<TBufferWriter>(ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
        {
            DequeueRangeAtMost(ref writer);
            if (writer.IsLengthConstrained && !writer.IsCompleted) throw new InvalidOperationException("Not enough data to write!");
        }
        public void DequeueRange<TBufferWriter>(ref TBufferWriter writer, nuint elements) where TBufferWriter : IBufferWriter<T>
        {
            var dw = DataWriter<T>.CreateFrom(ref writer, elements);
            DequeueRangeAtMost(ref dw);
            dw.Dispose();
        }
        public void DequeueRangeExact(NativeSpan<T> destination)
        {
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound;
            if (!destination.IsEmpty)
            {
                var wc = writeCursorCache.Load();
                var m = memory;
                var pos = rc - rcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var readable = ReadableItemsWithCache(wc, rc, destination.Length);
                if (readable >= destination.Length)
                {
                    var span = m.Span;
                    var readSpan0 = span.Slice(pos).SliceWhileIfLongerThan(readable);
                    var read = readSpan0.CopyAtMostTo(destination);
                    if (read < readable && read < destination.Length)
                    {
                        readable -= read;
                        var readSpan1 = span.SliceWhile(readable);
                        read += readSpan1.CopyAtMostTo(destination.Slice(read));
                    }
                    ThreadingExtensions.StoreFence();
                    AdvanceReadCursor(rc, ref rcl, capacity, read);
                }
                else
                {
                    throw new InvalidOperationException("Not enough data to write!");
                }
            }
        }
        public nuint DequeueRangeAtMost<TBufferWriter>(scoped ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
        {
            nuint written = 0;
            if (writer.IsCompleted) return written;
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound;
            var wc = writeCursorCache.Load();
            var m = memory;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var readable = ReadableItemsWithCache(wc, rc);
            while (!writer.IsCompleted && readable > 0)
            {
                var span = m.Span;
                var readSpan0 = span.Slice(pos).SliceWhileIfLongerThan(readable);
                var read = writer.WriteAtMost(readSpan0);
                if (written < readable && !writer.IsCompleted)
                {
                    readable -= read;
                    var readSpan1 = span.SliceWhile(readable);
                    read += writer.WriteAtMost(readSpan1);
                }
                written += read;
                rc = AdvanceReadCursor(rc, ref rcl, capacity, read);
                ThreadingExtensions.Yield();
                ThreadingExtensions.MemoryFence();
                wc = writeCursor.VolatileLoad();
                writeCursorCache.Set(wc);
                pos = rc - rcl;
                readable = wc - rc;
            }
            return written;
        }

        public nuint DequeueRangeAtMost(NativeSpan<T> destination)
        {
            nuint read = 0;
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound;
            if (!destination.IsEmpty)
            {
                var wc = writeCursorCache.Load();
                var m = memory;
                var pos = rc - rcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var readable = ReadableItemsWithCache(wc, rc, destination.Length);
                if (readable > 0)
                {
                    var span = m.Span;
                    var readSpan0 = span.Slice(pos).SliceWhileIfLongerThan(readable);
                    read = readSpan0.CopyAtMostTo(destination);
                    if (read < readable && read < destination.Length)
                    {
                        readable -= read;
                        var readSpan1 = span.SliceWhile(readable);
                        read += readSpan1.CopyAtMostTo(destination.Slice(read));
                    }
                    ThreadingExtensions.StoreFence();
                    AdvanceReadCursor(rc, ref rcl, capacity, read);
                }
            }
            return read;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsFullWithCache(nuint writeCursor, nuint readCursorCache, nuint capacity, nuint itemsToWrite = 1)
        {
            var rc = readCursorCache;
            var full = IsFull(rc, writeCursor, capacity, itemsToWrite);
            if (full)
            {
                rc = readCursor.VolatileLoad();
                full = IsFull(rc, writeCursor, capacity, itemsToWrite);
                this.readCursorCache = rc;
            }
            return full;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private nuint WritableItemsWithCache(nuint writeCursor, nuint readCursorCache, nuint capacity, nuint desiredItemsToWrite = 1)
        {
            var rc = readCursorCache;
            var writableCount = WritableItems(rc, writeCursor, capacity);
            if (writableCount < desiredItemsToWrite)
            {
                this.readCursorCache = rc = readCursor.VolatileLoad();
                writableCount = WritableItems(rc, writeCursor, capacity);
            }
            return writableCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsEmptyWithCache(nuint writeCursorCache, nuint readCursor, nuint itemsToRead = 1)
        {
            var wc = writeCursorCache;
            var full = IsEmpty(readCursor, wc, itemsToRead);
            if (full)
            {
                wc = writeCursor.VolatileLoad();
                this.writeCursorCache.Set(wc);
                full = IsEmpty(readCursor, wc, itemsToRead);
            }

            return full;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private nuint ReadableItemsWithCache(nuint writeCursorCache, nuint readCursor, nuint desiredItemsToRead = 1)
        {
            var wc = writeCursorCache;
            var readableCount = wc - readCursor;
            if (readableCount < desiredItemsToRead)
            {
                wc = writeCursor.VolatileLoad();
                this.writeCursorCache.Set(wc);
                readableCount = wc - readCursor;
            }
            return readableCount;
        }

        public IEnumerator<T> GetEnumerator()
        {
            var wc = writeCursor.VolatileLoad();
            var rcl = readCursorLowerBound;
            var rc = readCursor.VolatileLoad();
            var m = memory;
            var span = m.Span;
            var count = wc - rc;
            if (count > 0)
            {
                var pos = rc - rcl;
                var region0 = span.Slice(pos).SliceWhileIfLongerThan(count);
                var region1 = span.SliceWhile(count - region0.Length);
                return new CopiedValuesEnumerator<T>([.. region0, .. region1]);
            }
            else
            {
                return Enumerable.Empty<T>().GetEnumerator();
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void DiscardHead(nuint count) => throw new NotImplementedException();
        public nuint DiscardHeadAtMost(nuint count) => throw new NotImplementedException();
    }

    public sealed partial class BoundedNativeRingQueue<T> : IDisposable
    {
        private void Dispose(bool disposing)
        {
            if (!AtomicUtils.Exchange(ref disposedValue, true))
            {
                memory.Span.Clear();
                if (disposing)
                {
                    owner.Dispose();
                }
                memory = default;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    internal static class BoundedNativeRingQueue
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsEmpty(nuint readCursor, nuint writeCursor, nuint itemsToRead = 1) => writeCursor - readCursor < itemsToRead;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsFull(nuint readCursor, nuint writeCursor, nuint capacity, nuint itemsToWrite = 1) => writeCursor - readCursor >= capacity - itemsToWrite;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static nuint WritableItems(nuint readCursor, nuint writeCursor, nuint capacity)
        {
            var count = writeCursor - readCursor;
            if (count >= capacity - 1) count = capacity - 1;
            return capacity - 1 - count;
        }
    }
}
