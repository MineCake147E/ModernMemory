using System;
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
    [CollectionBuilder(typeof(NativeCollectionBuilder), nameof(NativeCollectionBuilder.CreateNativePile))]
    public sealed class NativePile<T> : INativeList<T>, IDisposable
    {
        private DisposableValueSpinLockSlim mutateLock = new();
        private nuint count = 0;
        private MemoryResizer<T> resizer;

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
            if (acquiredLock.IsHolding)
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
            if (acquiredLock.IsHolding)
            {
                EnsureCapacityToAddInternal(items.Length);
                var m = items.CopyAtMostTo(Writable);
                count += m;
                Debug.Assert(m == items.Length);
            }
        }

        public bool TryAdd(T item)
        {
            using var acquiredLock = mutateLock.TryEnter();
            if (acquiredLock.IsHolding)
            {
                EnsureCapacityToAddInternal(1);
                Writable[0] = item;
                count++;
            }
            return acquiredLock.IsHolding;
        }

        public bool TryAdd(ReadOnlySpan<T> items) => TryAdd((ReadOnlyNativeSpan<T>)items);

        public bool TryAdd(ReadOnlyNativeSpan<T> items)
        {
            if (items.IsEmpty) return true;
            using var acquiredLock = mutateLock.TryEnter();
            if (acquiredLock.IsHolding)
            {
                EnsureCapacityToAddInternal(items.Length);
                var m = items.CopyAtMostTo(Writable);
                count += m;
                Debug.Assert(m == items.Length);
            }
            return acquiredLock.IsHolding;
        }

        public void Clear()
        {
            using var acquiredLock = mutateLock.Enter();
            if (acquiredLock.IsHolding)
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

        public DisposableValueSpinLockSlim.AcquiredLock GetAddBufferAtMost(out NativeSpan<T> buffer, nuint count)
        {
            var a = mutateLock.Enter();
            var destination = Writable;
            buffer = destination.SliceWhileIfLongerThan(count);
            this.count += count;
            return a;
        }

        public bool IsLocked => mutateLock.IsHeld;

        public DisposableValueSpinLockSlim.AcquiredLock EnterLockForMutation() => mutateLock.Enter();

        public nuint PurgeItemsBy(Predicate<T> predicate)
        {
            using var l = mutateLock.Enter();
            var vv = VisibleValues;
            if (vv.IsEmpty || predicate is null) return 0;
            var current = vv.Length;
            var lastSurvived = current - 1;
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
            var current = vv.Length;
            var lastSurvived = current - 1;
            var purgeCountLimited = writer.TryGetRemainingElementsToWrite(out var allowedPurges);
            if (!purgeCountLimited || allowedPurges > current) allowedPurges = current;
            while (--current < vv.Length)
            {
                ref var currentItem = ref vv[current];
                ref var lastSurvivedItem = ref vv[lastSurvived];
                if (predicate(currentItem))
                {
                    // We need to swap them in order to write purged values to buffer.
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

        public bool TryPop(out T? value)
        {
            Unsafe.SkipInit(out value);
            using var l = mutateLock.TryEnter();
            var vv = VisibleValues;
            var v = l.IsHolding && !vv.IsEmpty;
            if (v)
            {
                value = vv.Tail;
            }
            if (v && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                vv.Tail = default!;
            }
            count -= v ? 1u : 0;
            return v;
        }

        public bool TryPop(out T? value, ulong spinCount)
        {
            Unsafe.SkipInit(out value);
            using var l = mutateLock.TryEnterBySpin(spinCount);
            var vv = VisibleValues;
            var v = l.IsHolding && !vv.IsEmpty;
            if (v)
            {
                value = vv.Tail;
            }
            if (v && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                vv.Tail = default!;
            }
            count -= v ? 1u : 0;
            return v;
        }

        public bool TryPopSpinning(out T? value)
        {
            value = default;
            if (count == 0) return false;
            using var l = mutateLock.Enter();
            var vv = VisibleValues;
            var v = !vv.IsEmpty;
            if (v)
            {
                value = vv.Tail;
            }
            if (v && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                vv.Tail = default!;
            }
            count -= v ? 1u : 0;
            return v;
        }

        public bool TryPopSpinningWhileNotNull([NotNullWhen(true)] out T? value)
        {
            T? res = default;
            if (count > 0)
            {
                using var l = mutateLock.Enter();
                var vv = VisibleValues;
                var c = vv.Length - 1;
                var c2 = c;
                if (c2 < vv.Length)
                {
                    do
                    {
                        c = c2;
                        res = vv.ElementAtUnchecked(c2);
                    } while (res is null && --c2 < vv.Length);
                    count = c;
                    if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                    {
                        vv.Slice(c).Clear();
                    }
                }
            }
            value = res;
            return res is not null;
        }

        public void PurgeTail(nuint count)
        {
            using var l = mutateLock.Enter();
            var vv = VisibleValues;
            if (vv.Length < count) count = vv.Length;
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                vv.Slice(vv.Length - count).Clear();
            }
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
