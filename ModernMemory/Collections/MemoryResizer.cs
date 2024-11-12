using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Buffers.Pooling;
using ModernMemory.Collections.Storage;

namespace ModernMemory.Collections
{
    public struct MemoryResizer<T> : IResizableCollectionStorage<T>
    {
#pragma warning disable IDE0032 // Use auto property
        private NativeMemory<T> nativeMemory;
#pragma warning restore IDE0032 // Use auto property
        private MemoryOwnerContainer<T> owner;
        private NativeMemoryPool<T>? pool;

        public MemoryResizer()
        {
            this = new(NativeMemoryPool<T>.Shared);
        }

        public MemoryResizer(nuint initialSize)
        {
            this = new(NativeMemoryPool<T>.Shared, initialSize);
        }

        public MemoryResizer(ReadOnlyNativeSpan<T> initialElements)
        {
            this = new(NativeMemoryPool<T>.Shared, initialElements);
        }

        public MemoryResizer(NativeMemoryPool<T> pool)
        {
            ArgumentNullException.ThrowIfNull(pool);
            this.pool = pool;
            owner = pool.RentWithDefaultSize();
            nativeMemory = owner.NativeMemory;
        }

        public MemoryResizer(NativeMemoryPool<T> pool, nuint initialSize)
        {
            ArgumentNullException.ThrowIfNull(pool);
            this.pool = pool;
            owner = pool.Rent(initialSize);
            nativeMemory = owner.NativeMemory;
        }

        public MemoryResizer(NativeMemoryPool<T> pool, ReadOnlyNativeSpan<T> initialElements)
        {
            ArgumentNullException.ThrowIfNull(pool);
            this.pool = pool;
            var values = initialElements;
            owner = pool.Rent(values.Length);
            nativeMemory = owner.NativeMemory;
            initialElements.CopyTo(owner.Span);
        }

        public readonly NativeMemory<T> Memory => nativeMemory;

        public readonly bool IsUninitialized => pool is null;

        public readonly NativeSpan<T> Span => nativeMemory.Span;

        public static void LazyInit(ref MemoryResizer<T> resizer)
        {
            if (resizer.IsUninitialized) resizer = new();
        }

        public static void LazyInit(ref MemoryResizer<T> resizer, ReadOnlyNativeSpan<T> values)
        {
            var initialSize = values.Length;
            if (!resizer.IsUninitialized)
            {
                resizer.Resize(initialSize, values);
                return;
            }
            resizer = new(initialSize);
            values.CopyTo(resizer.nativeMemory.Span);
        }

        public static void LazyInit(ref MemoryResizer<T> resizer, nuint initialSize, ReadOnlyNativeSpan<T> values = default)
        {
            if (!resizer.IsUninitialized)
            {
                resizer.Resize(initialSize, values);
                return;
            }
            resizer = new(initialSize);
            values.CopyTo(resizer.nativeMemory.Span);
        }

        public static void LazyInit(ref MemoryResizer<T> resizer, NativeMemoryPool<T> pool)
        {
            if (resizer.IsUninitialized) resizer = new(pool);
        }

        public static void LazyInit(ref MemoryResizer<T> resizer, NativeMemoryPool<T> pool, nuint initialSize, ReadOnlyNativeSpan<T> values = default)
        {
            if (!resizer.IsUninitialized)
            {
                resizer.Resize(initialSize, values);
                return;
            }
            resizer = new(pool, initialSize);
            values.CopyTo(resizer.nativeMemory.Span);
        }

        public bool Resize(nuint newSize, out MemoryOwnerContainer<T> oldOwner)
        {
            var oldData = nativeMemory.Span;
            MemoryOwnerContainer<T> ownerSwapped = default;
            if (newSize > oldData.Length)
            {
                var destination = Expand(newSize, out ownerSwapped);
                _ = oldData.CopyAtMostTo(destination);
            }
            oldOwner = ownerSwapped;
            return !ownerSwapped.IsOwnerNull;
        }

        public void Resize(nuint newSize)
        {
            if (Resize(newSize, out var ownerToDispose))
            {
                ownerToDispose.Span.ClearIfReferenceOrContainsReferences();
                ownerToDispose.Dispose();
            }
        }

        public bool Resize(ReadOnlyNativeSpan<T> values, out MemoryOwnerContainer<T> oldOwner) => Resize(values.Length, values, out oldOwner);

        public void Resize(ReadOnlyNativeSpan<T> values)
        {
            if (Resize(values.Length, values, out var ownerToDispose))
            {
                ownerToDispose.Span.ClearIfReferenceOrContainsReferences();
                ownerToDispose.Dispose();
            }
        }

        public bool Resize(nuint newSize, ReadOnlyNativeSpan<T> values, out MemoryOwnerContainer<T> oldOwner)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(newSize, values.Length);
            var destination = nativeMemory.Span;
            MemoryOwnerContainer<T> ownerSwapped = default;
            if (newSize > destination.Length)
            {
                destination = Expand(newSize, out ownerSwapped);
            }
            _ = values.CopyAtMostTo(destination);
            oldOwner = ownerSwapped;
            return !ownerSwapped.IsOwnerNull;
        }

        public void Resize(nuint newSize, ReadOnlyNativeSpan<T> values)
        {
            if (Resize(newSize, values, out var ownerToDispose))
            {
                ownerToDispose.Span.ClearIfReferenceOrContainsReferences();
                ownerToDispose.Dispose();
            }
        }

        private NativeSpan<T> Expand(nuint newSize, out MemoryOwnerContainer<T> oldOwner)
        {
            ObjectDisposedException.ThrowIf(pool is null, this);
            var newOwner = pool.Rent(newSize);
            var newMemory = newOwner.NativeMemory;
            if (newMemory.Length < newSize)
                throw new InvalidOperationException($"Rent returned a buffer with size smaller than required! (expected: {newSize}, actual: {newMemory.Length})");
            oldOwner = owner;
            nativeMemory = newMemory;
            owner = newOwner;
            return newMemory.Span;
        }

        private void Dispose(bool disposing)
        {
            var localPool = Interlocked.Exchange(ref pool, null);
            if (localPool is not null)
            {
                if (disposing)
                {
                    nativeMemory.Span.ClearIfReferenceOrContainsReferences();
                    owner.Dispose();
                }
                nativeMemory = default;
                owner = default;
            }
        }

        public void Dispose() => Dispose(disposing: true);
    }
}
