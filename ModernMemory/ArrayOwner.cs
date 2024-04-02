using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Collections;

namespace ModernMemory
{
    public sealed class ArrayOwner<T> : INativeMemoryOwner<T>, INativeIndexable<T>, IMemoryEnumerable<T>, ISpanEnumerable<T>
    {
#pragma warning disable IDE0032 // Use auto property
        private NativeMemory<T> nativeMemory;
#pragma warning restore IDE0032 // Use auto property
        private INativeMemoryOwner<T>? owner;
        private bool disposedValue;

        public NativeMemory<T> NativeMemory => nativeMemory;

        public static ArrayOwner<T> Empty { get; } = new(true);

        public ArrayOwner(INativeMemoryOwner<T>? owner)
        {
            Owner = owner;
        }

        public ArrayOwner(nuint minimumLength)
        {
            Owner = NativeMemoryPool<T>.Shared.Rent(minimumLength);
        }

        private ArrayOwner(bool empty = false)
        {
            if (empty)
            {
                owner = null;
                nativeMemory = default;
                Dispose();
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
                nativeMemory = owner?.NativeMemory ?? default;
                owner = value;
            }
        }

        Memory<T> IMemoryOwner<T>.Memory => owner?.Memory ?? default;
        nuint ICountable<nuint>.Count => Length;

        public T this[nuint index] { get => Span[index]; set => Span[index] = value; }

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Debug.Assert(disposing || !disposedValue);
                var span = Span;
                if (!span.IsEmpty && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                    span.Clear();
                Owner?.Dispose();
                Owner = null;
                disposedValue = true;
            }
        }

        ~ArrayOwner()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ReadOnlyNativeMemory<T>.Enumerator ITypedEnumerable<T, ReadOnlyNativeMemory<T>.Enumerator>.GetEnumerator() => new(NativeMemory);
        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => new(Span);
    }
}
