using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections.Storage;
using ModernMemory.DataFlow;
using ModernMemory.Threading;

using static ModernMemory.Collections.Concurrent.BoundedNativeRingQueue;

namespace ModernMemory.Collections.Concurrent
{
    [StructLayout(LayoutKind.Sequential, Pack = 64)]
    internal partial struct BoundedNativeRingQueueCore<T, TStorage> : IQueue<T>, IEnumerable<T>
        where TStorage : ICollectionStorage<T>
    {
#pragma warning disable S1144 // Unused private types or members should be removed (needed for padding to prevent false sharing)
        private readonly Vector512<byte> padding0 = default;
#pragma warning restore S1144 // Unused private types or members should be removed
        private PaddedUIntPtr readCursorLowerBound = new();
        private PaddedUIntPtr writeCursorCache = new();
        private PaddedUIntPtr readCursor = new();
        private uint disposedValue = AtomicUtils.GetValue(false);
#pragma warning disable S1144 // Unused private types or members should be removed (needed for padding to prevent false sharing)
        private readonly Vector512<byte> padding1 = default;
#pragma warning restore S1144 // Unused private types or members should be removed
        private PaddedUIntPtr writeCursorLowerBound = new();
        private PaddedUIntPtr writeCursor = new();
        private PaddedUIntPtr readCursorCache = new();
        private TStorage storage;

        internal NativeSpan<T> Span => storage.Span;

        public BoundedNativeRingQueueCore(TStorage storage)
        {
            ArgumentNullException.ThrowIfNull(storage);
            this.storage = storage;
        }

        public BoundedNativeRingQueueCore(TStorage storage, ReadOnlyNativeSpan<T> initialValues)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(initialValues.Length, storage.Span.Length);
            this.storage = storage;
            var span = storage.Span;
            var writeHead = initialValues.CopyAtMostTo(span);
            writeCursor.Set(writeHead);
            writeCursorCache.Set(writeHead);
        }

        public nuint Capacity => storage.Span.Length - 1;

        public nuint Count => writeCursor.VolatileLoad() - readCursor.VolatileLoad();

        public T this[nuint index]
        {
            get
            {
                var rc = readCursor.Load();
                var rcl = readCursorLowerBound.Load();
                var wc = writeCursorCache.Load();
                var m = storage.Span;
                var pos = rc - rcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var readable = ReadableItemsWithCache(wc, rc, index + 1);
                if (readable > 0)
                {
                    var span = m;
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
            var rcl = readCursorLowerBound.Load();
            AdvanceReadCursor(rc, ref rcl, storage.Span.Length, wc - rc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceWriteCursor(nuint wc, nuint wcl, nuint capacity, nuint written)
        {
            wc += written;
            var ncl = wcl + capacity;
            writeCursor.Set(wc);
            if (wc < ncl)
            {
                return;
            }
            writeCursorLowerBound.Set(ncl);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private nuint AdvanceReadCursor(nuint rc, ref nuint rcl, nuint capacity, nuint read)
        {
            rc += read;
            var ncl = rcl + capacity;
            readCursor.Set(rc);
            if (rc >= ncl)
            {
                rcl = ncl;
                readCursorLowerBound.Set(ncl);
            }
            return rc;
        }

        [SkipLocalsInit]
        public bool TryAdd(T item) => TryAddInternal(item, storage.Span);

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryAddInternal(T item, NativeSpan<T> s)
        {
            var wc = writeCursor.Load();
            var rc = readCursorCache.Load();
            var wcl = writeCursorLowerBound.Load();
            var capacity = s.Length;
            var pos = wc - wcl;
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

        [SkipLocalsInit]
        public nuint AddAtMost(ReadOnlyNativeSpan<T> items) => AddAtMostInternal(items, storage.Span);

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal nuint AddAtMostInternal(ReadOnlyNativeSpan<T> items, NativeSpan<T> span)
        {
            var wc = writeCursor.Load();
            var wcl = writeCursorLowerBound.Load();
            var capacity = span.Length;
            nuint written = 0;
            if (!items.IsEmpty)
            {
                var rc = readCursorCache.Load();
                var pos = wc - wcl;
                Debug.Assert(pos < capacity);
                var writableCount = WritableItemsWithCache(wc, rc, capacity, items.Length);
                if (writableCount > 0)
                {
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

        public void Add(ReadOnlyNativeSpan<T> items) => AddInternal(items, storage.Span);

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddInternal(ReadOnlyNativeSpan<T> items, NativeSpan<T> span)
        {
            var wc = writeCursor.Load();
            var wcl = writeCursorLowerBound.Load();
            if (!items.IsEmpty)
            {
                var rc = readCursorCache.Load();
                var pos = wc - wcl;
                var capacity = span.Length;
                Debug.Assert(pos < capacity);
                var writableCount = WritableItemsWithCache(wc, rc, capacity, items.Length);
                if (writableCount >= items.Length)
                {
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
            var rcl = readCursorLowerBound.Load();
            var wc = writeCursorCache.Load();
            var m = storage.Span;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var empty = IsEmptyWithCache(wc, rc);
            success = !empty;
            ref var src = ref Unsafe.NullRef<T>();
            if (!empty) src = ref m[pos]!;
            return ref src;
        }

        public bool TryPeek(out T? item)
        {
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound.Load();
            var wc = writeCursorCache.Load();
            var m = storage.Span;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var empty = IsEmptyWithCache(wc, rc);
            item = !empty ? m[pos] : default;
            return !empty;
        }

        public T? Peek() => TryPeek(out var item) ? item : throw new InvalidOperationException("Tried to peek the queue while it's empty!");

        public nuint PeekRangeAtMost(NativeSpan<T> destination)
        {
            nuint read = 0;
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound.Load();
            if (!destination.IsEmpty)
            {
                var wc = writeCursorCache.Load();
                var m = storage.Span;
                var pos = rc - rcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var readable = ReadableItemsWithCache(wc, rc, destination.Length);
                if (readable > 0)
                {
                    var span = m;
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
            var rcl = readCursorLowerBound.Load();
            var wc = writeCursorCache.Load();
            var m = storage.Span;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var empty = IsEmptyWithCache(wc, rc);
            if (!empty)
            {
                item = m[pos];
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
            var rcl = readCursorLowerBound.Load();
            if (!destination.IsEmpty)
            {
                var wc = writeCursorCache.Load();
                var m = storage.Span;
                var pos = rc - rcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var readable = ReadableItemsWithCache(wc, rc, destination.Length);
                if (readable >= destination.Length)
                {
                    var span = m;
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
            var rcl = readCursorLowerBound.Load();
            var wc = writeCursorCache.Load();
            var m = storage.Span;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var readable = ReadableItemsWithCache(wc, rc);
            while (!writer.IsCompleted && readable > 0)
            {
                var span = m;
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
            var rcl = readCursorLowerBound.Load();
            if (!destination.IsEmpty)
            {
                var wc = writeCursorCache.Load();
                var m = storage.Span;
                var pos = rc - rcl;
                var capacity = m.Length;
                Debug.Assert(pos < capacity);
                var readable = ReadableItemsWithCache(wc, rc, destination.Length);
                if (readable > 0)
                {
                    var span = m;
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
                this.readCursorCache.Set(rc);
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
                rc = readCursor.VolatileLoad();
                this.readCursorCache.Set(rc);
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
            var rcl = readCursorLowerBound.Load();
            var rc = readCursor.VolatileLoad();
            var m = storage.Span;
            var span = m;
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

        public void DiscardHead(nuint count)
        {
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound.Load();
            var wc = writeCursorCache.Load();
            var m = storage.Span;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var readable = ReadableItemsWithCache(wc, rc);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, readable);
            var span = m;
            var readSpan0 = nuint.Min(readable, span.Length - pos);
            var read = nuint.Min(count, readSpan0);
            if (read < count)
            {
                readable -= read;
                count -= read;
                read += nuint.Min(count, readable);
            }
            AdvanceReadCursor(rc, ref rcl, capacity, read);
        }

        public nuint DiscardHeadAtMost(nuint count)
        {
            nuint written = 0;
            var rc = readCursor.Load();
            var rcl = readCursorLowerBound.Load();
            var wc = writeCursorCache.Load();
            var m = storage.Span;
            var pos = rc - rcl;
            var capacity = m.Length;
            Debug.Assert(pos < capacity);
            var readable = ReadableItemsWithCache(wc, rc);
            if (readable > 0)
            {
                var span = m;
                var readSpan0 = nuint.Min(readable, span.Length - pos);
                var read = nuint.Min(count, readSpan0);
                if (read < count)
                {
                    readable -= read;
                    count -= read;
                    read += nuint.Min(count, readable);
                }
                written += read;
                AdvanceReadCursor(rc, ref rcl, capacity, read);
            }
            return written;
        }
    }

    internal partial struct BoundedNativeRingQueueCore<T, TStorage>
    {
        internal bool DisposeCore(bool disposing)
        {
            var res = !AtomicUtils.Exchange(ref disposedValue, true);
            if (res)
            {
                storage.Span.Clear();
                if (disposing)
                {
                    storage.Dispose();
                }
                storage = default!;
            }
            return res;
        }
    }

    public static partial class BoundedNativeRingQueue
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
