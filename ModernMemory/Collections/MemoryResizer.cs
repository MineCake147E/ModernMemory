using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Collections
{
    public struct MemoryResizer<T> : IDisposable
    {
#pragma warning disable IDE0032 // Use auto property
        private NativeMemory<T> nativeMemory;
#pragma warning restore IDE0032 // Use auto property
        private INativeMemoryOwner<T>? owner;
        private NativeMemoryPool<T>? pool;
        private bool disposedValue;

        public MemoryResizer()
        {
            this = new(NativeMemoryPool<T>.Shared);
        }

        public MemoryResizer(nuint initialSize)
        {
            this = new(NativeMemoryPool<T>.Shared, initialSize);
        }

        public MemoryResizer(NativeMemoryPool<T> pool)
        {
            ArgumentNullException.ThrowIfNull(pool);
            this.pool = pool;
            owner = pool.RentWithDefaultSize();
        }

        public MemoryResizer(NativeMemoryPool<T> pool, nuint initialSize)
        {
            ArgumentNullException.ThrowIfNull(pool);
            this.pool = pool;
            owner = pool.Rent(initialSize);
        }

        public readonly NativeMemory<T> NativeMemory => nativeMemory;

        public readonly bool IsUninitialized => pool is null;

        public static void LazyInit(ref MemoryResizer<T> resizer)
        {
            if (resizer.IsUninitialized) resizer = new();
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

        public void Resize(nuint newSize)
        {
            var oldMemory = nativeMemory;
            if (newSize <= oldMemory.Length) return;
            ObjectDisposedException.ThrowIf(pool is null, this);
            var newOwner = pool.Rent(newSize) ?? throw new InvalidOperationException("Rent returned null!");
            var newMemory = newOwner.NativeMemory;
            if (newMemory.Length < newSize)
                throw new InvalidOperationException($"Rent returned a buffer with size smaller than required! (expected: {newSize}, actual: {newMemory.Length})");
            oldMemory.CopyTo(newMemory);
            var oldOwner = owner;
            nativeMemory = newMemory;
            owner = newOwner;
            oldOwner?.Dispose();
        }

        public void Resize(nuint newSize, ReadOnlyNativeSpan<T> values)
        {
            var oldMemory = nativeMemory;
            if (newSize <= oldMemory.Length)
            {
                values.CopyTo(oldMemory.Span);
                return;
            }
            ObjectDisposedException.ThrowIf(pool is null, this);
            var newOwner = pool.Rent(newSize) ?? throw new InvalidOperationException("Rent returned null!");
            var newMemory = newOwner.NativeMemory;
            if (newMemory.Length < newSize)
                throw new InvalidOperationException($"Rent returned a buffer with size smaller than required! (expected: {newSize}, actual: {newMemory.Length})");
            values.CopyTo(newMemory.Span);
            var oldOwner = owner;
            nativeMemory = newMemory;
            owner = newOwner;
            oldOwner?.Dispose();
        }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    nativeMemory.Span.ClearIfReferenceOrContainsReferences();
                    owner?.Dispose();
                }
                nativeMemory = default;
                owner = null;
                pool = null;
                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(disposing: true);
    }
}
