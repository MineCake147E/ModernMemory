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
using ModernMemory.DataFlow;
using ModernMemory.Threading;

namespace ModernMemory.Collections
{
    [CollectionBuilder(typeof(NativeCollectionBuilder), nameof(NativeCollectionBuilder.CreateNativePile))]
    public sealed class NativePile<T> : INativeList<T>, IDisposable
    {
        private DisposableValueSpinLockSlim mutateLock = new();
        private nuint count = 0;
        MemoryResizer<T> resizer;

        internal NativeMemory<T> NativeMemory => resizer.NativeMemory;

        internal NativeSpan<T> VisibleValues => NativeMemory.Span.Slice(0, count);

        internal NativeSpan<T> Writable => NativeMemory.Span.Slice(count);

        public nuint Count => count;

        public NativeMemory<T> Memory => NativeMemory.Slice(0, count);

        public NativeSpan<T> Span => VisibleValues;

        public T this[nuint index] { get => VisibleValues[index]; set => VisibleValues[index] = value; }

        internal NativePile(MemoryResizer<T> resizer)
        {
            this.resizer = resizer;
        }

        public NativePile() : this(new MemoryResizer<T>()) { }

        public NativePile(ReadOnlyNativeSpan<T> values) : this(values.Length)
        {
            Add(values);
        }

        public NativePile(NativeMemoryPool<T> pool) : this(new MemoryResizer<T>(pool)) { }

        public NativePile(nuint initialSize) : this(new MemoryResizer<T>(initialSize)) { }
        public NativePile(NativeMemoryPool<T> pool, nuint initialSize) : this(new MemoryResizer<T>(pool, initialSize)) { }

        public void Add(T item)
        {
            using var acquiredLock = mutateLock.Enter();
            if (acquiredLock.IsHeld)
            {
                EnsureCapacityToAddInternal(1);
                Writable[0] = item;
                count++;
            }
        }

        public void Add(ReadOnlySpan<T> items) => Add((ReadOnlyNativeSpan<T>)items);

        public void Add(ReadOnlyNativeSpan<T> items)
        {
            if (items.IsEmpty) return;
            using var acquiredLock = mutateLock.Enter();
            if (acquiredLock.IsHeld)
            {
                EnsureCapacityToAddInternal(items.Length);
                var m = items.CopyAtMostTo(Writable);
                count += m;
                Debug.Assert(m == items.Length);
            }
        }

        public void Clear()
        {
            using var acquiredLock = mutateLock.Enter();
            if (acquiredLock.IsHeld)
            {
                VisibleValues.ClearIfReferenceOrContainsReferences();
                count = 0;
            }
        }

        public void EnsureCapacityToAdd(nuint size)
        {
            using var acquiredLock = mutateLock.Enter();
            EnsureCapacityToAddInternal(size);
        }

        public bool IsLocked => mutateLock.IsHeld;

        public DisposableValueSpinLockSlim.AcquiredLock EnterLockForMutation() => mutateLock.Enter();

        public nuint PurgeItemsBy(Predicate<T> predicate)
        {
            using var l = mutateLock.Enter();
            var vv = VisibleValues;
            if (vv.IsEmpty || predicate is null) return 0;
            nuint current = vv.Length;
            nuint lastSurvived = current - 1;
            while (--current < vv.Length)
            {
                ref var currentItem = ref vv[current];
                ref var lastSurvivedItem = ref vv[lastSurvived];
                if (predicate(currentItem))
                {
                    currentItem = lastSurvivedItem;
                    lastSurvived--;
                }
            }
            var newCount = lastSurvived + 1;
            count = newCount;
            vv.Slice(newCount).ClearIfReferenceOrContainsReferences();
            return vv.Length - newCount;
        }

        public nuint PurgeItemsBy<TBufferWriter>(Predicate<T> predicate, scoped ref DataWriter<T, TBufferWriter> writer) where TBufferWriter : IBufferWriter<T>
        {
            using var l = mutateLock.Enter();
            var vv = VisibleValues;
            if (vv.IsEmpty || predicate is null) return 0;
            nuint current = vv.Length;
            nuint lastSurvived = current - 1;
            var purgeCountLimited = writer.TryGetRemainingElementsToWrite(out var allowedPurges);
            if (!purgeCountLimited || allowedPurges > current) allowedPurges = current;
            while (--current < vv.Length)
            {
                ref var currentItem = ref vv[current];
                ref var lastSurvivedItem = ref vv[lastSurvived];
                if (predicate(currentItem))
                {
                    // We need to swap them in order to write purged values to writer.
                    (lastSurvivedItem, currentItem) = (currentItem, lastSurvivedItem);
                    lastSurvived--;
                    if (vv.Length - lastSurvived > allowedPurges) break;
                }
            }
            var newCount = lastSurvived + 1;
            count = newCount;
            var s = vv.Slice(newCount);
            var w = writer.WriteAtMost(s);
            Debug.Assert(w == s.Length);
            s.ClearIfReferenceOrContainsReferences();
            return s.Length;
        }

        public void PurgeTail(nuint count)
        {
            using var l = mutateLock.Enter();
            var vv = VisibleValues;
            if (vv.Length < count) count = vv.Length;
            vv.Slice(vv.Length - count).ClearIfReferenceOrContainsReferences();
            this.count = vv.Length - count;
        }

        public void PurgeHead(nuint count)
        {
            using var l = mutateLock.Enter();
            var vv = VisibleValues;
            if (vv.Length < count) count = vv.Length;
            var h = vv.Slice(count).CopyAtMostTo(vv);
            vv.Slice(h).ClearIfReferenceOrContainsReferences();
            this.count = vv.Length - count;
        }

        private void EnsureCapacityToAddInternal(nuint size)
        {
            if (Writable.Length >= size)
            {
                return;
            }
            ExpandIfNeeded(size);
        }

        private void ExpandIfNeeded(nuint size)
        {
            LazyTrimHead(size);
            Debug.Assert(Writable.Length >= size);
        }

        private void LazyTrimHead(nuint addingElements)
        {
            var m = NativeMemory;
            var c = count;
            if (c == 0)
            {
                c = 0;
            }
            var span = m.Span;
            var v = span.Slice(0, c);
            var newSize = c + addingElements;
            resizer.Resize(newSize, v);
            count = c;
        }

        public IEnumerator<T> GetEnumerator() => new CopiedValuesEnumerator<T>(VisibleValues);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void Dispose(bool disposing)
        {
            var acquiredLock = mutateLock.Enter(out var isDisposed);
            if (!isDisposed)
            {
                if (disposing || RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    resizer.NativeMemory.Span.Clear();
                }
                resizer.Dispose();
                resizer = default;
                acquiredLock.DisposeLock();
            }
            else
            {
                acquiredLock.Dispose();
            }
        }

        ~NativePile()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
