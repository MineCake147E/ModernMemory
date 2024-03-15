using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Collections
{
    public sealed class PooledArray<T> : INativeMemoryOwner<T>
    {
        public NativeMemory<T> NativeMemory { get; private set; }

        private INativeMemoryOwner<T>? owner;
        private bool disposedValue;

        public static PooledArray<T> Empty { get; } = new(true);

        public PooledArray(INativeMemoryOwner<T>? owner)
        {
            Owner = owner;
        }

        public PooledArray(nuint minimumLength)
        {
            Owner = NativeMemoryPool<T>.Shared.Rent(minimumLength);
        }

        private PooledArray(bool empty = false)
        {
            if (empty)
            {
                owner = null;
                NativeMemory = default;
                disposedValue = true;
#pragma warning disable S3971 // "GC.SuppressFinalize" should not be called
                GC.SuppressFinalize(this);
#pragma warning restore S3971 // "GC.SuppressFinalize" should not be called
            }
        }

        public nuint Length => NativeMemory.Length;

        public NativeSpan<T> Span => NativeMemory.Span;

        public void Clear() => Span.Clear();

        private INativeMemoryOwner<T>? Owner
        {
            get => owner;
            set
            {
                NativeMemory = owner?.NativeMemory ?? default;
                owner = value;
            }
        }

        Memory<T> IMemoryOwner<T>.Memory { get; }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Debug.Assert(disposing || !disposedValue);
                var span = Span;
                if (!span.IsEmpty && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    span.Clear();
                }
                Owner?.Dispose();
                Owner = null;
                disposedValue = true;
            }
        }

        ~PooledArray()
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
