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
using ModernMemory.Buffers.Pooling;
using ModernMemory.Collections;
using ModernMemory.Threading;

namespace ModernMemory
{
    public sealed class ArrayOwner<T> : INativeMemoryOwner<T>, INativeIndexable<T>, IMemoryEnumerable<T>, ISpanEnumerable<T>
    {
#pragma warning disable IDE0032 // Use auto property
        private NativeMemory<T> nativeMemory;
#pragma warning restore IDE0032 // Use auto property
        private MemoryOwnerContainer<T> owner;
        private uint disposedValue = AtomicUtils.GetValue(false);

        public NativeMemory<T> NativeMemory => nativeMemory;

        public static ArrayOwner<T> Empty { get; } = new(true);

        public ArrayOwner(INativeMemoryOwner<T>? owner)
        {
            SetOwner(new(owner));
        }

        internal ArrayOwner(MemoryOwnerContainer<T> owner)
        {
            SetOwner(owner);
        }

        public ArrayOwner(nuint minimumLength) : this(minimumLength, NativeMemoryPool<T>.Shared) { }

        public ArrayOwner(nuint minimumLength, NativeMemoryPool<T> pool)
        {
            SetOwner(pool.Rent(minimumLength));
        }

        private ArrayOwner(bool empty = false)
        {
            if (empty)
            {
                owner = default;
                nativeMemory = default;
                Dispose();
            }
        }

        public nuint Length => NativeMemory.Length;

        public NativeSpan<T> Span => owner.Span;

        public void Clear() => Span.Clear();


        private void SetOwner(MemoryOwnerContainer<T> value)
        {
            nativeMemory = value.NativeMemory;
            owner = value;
        }

        Memory<T> IMemoryOwner<T>.Memory => owner.Memory;
        nuint ICountable<nuint>.Count => Length;

        public T this[nuint index] { get => Span[index]; set => Span[index] = value; }

        private void Dispose(bool disposing)
        {
            if (!AtomicUtils.Exchange(ref disposedValue, true))
            {
                Debug.Assert(disposing);
                var span = Span;
                span.ClearIfReferenceOrContainsReferences();
                owner.Dispose();
                owner = default;
                nativeMemory = default;
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
