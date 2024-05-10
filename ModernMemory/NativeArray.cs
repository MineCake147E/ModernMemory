using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ModernMemory.Buffers;
using ModernMemory.Collections;

namespace ModernMemory
{
    /// <summary>
    /// Represents a native memory, that can hold more data than <see cref="Array"/> can.
    /// </summary>
    /// <typeparam name="T">The type of items in the <see cref="NativeArray{T}"/>.</typeparam>
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    [Obsolete("Use ArrayOwner instead!")]
    [StructLayout(LayoutKind.Sequential)]
    public sealed unsafe class NativeArray<T> : INativeSpanFactory<T>, IReadOnlyList<T>, ITypedEnumerable<T, NativeArray<T>.Enumerator>, IDisposable
    {
        private T* head;
        private nuint length;
        private bool disposedValue;

        // TODO: Refactor the whole thing to implement NativeMemoryManager

        /// <summary>
        /// The length of this <see cref="NativeArray{T}"/>.
        /// </summary>
        public nuint Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => length;
            private init => length = value;
        }

        private readonly byte alignmentExponent;

        /// <summary>
        /// Gets the current alignment in bytes.
        /// </summary>
        public nuint CurrentAlignment => (nuint)1 << BitOperations.TrailingZeroCount((nuint)head);

        /// <summary>
        /// Gets the initially desired alignment in bytes.
        /// </summary>
        public nuint RequestedAlignment => (nuint)1 << alignmentExponent;

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> object over the entirety of the current <see cref="NativeArray{T}"/>.
        /// </summary>
        public NativeSpan<T> NativeSpan => new(ref *head, Length);

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> object over the entirety of the current <see cref="NativeArray{T}"/>.
        /// </summary>
        public ReadOnlyNativeSpan<T> ReadOnlyNativeSpan => new(ref *head, Length);

        /// <summary>
        /// Returns a internalValue that indicates whether the current <see cref="NativeArray{T}"/> is empty.
        /// </summary>
        public bool IsEmpty => Length <= 0;

        /// <summary>
        /// Returns an empty <see cref="NativeArray{T}"/>.
        /// </summary>
        public static NativeArray<T> Empty { get; } = new(true);

        /// <summary>
        /// Gets the theoretical maximum length of <see cref="NativeArray{T}"/>.
        /// </summary>
        public static nuint TheoreticalMaxLength => NativeMemoryUtils.MaxSizeForType<T>();

        int IReadOnlyCollection<T>.Count => (int)nuint.Max(int.MaxValue, Length);

        T IReadOnlyList<T>.this[int index] => ElementAtChecked(index);

        public T this[nuint index]
        {
            get => ElementAtChecked(index);
            set => ElementAtChecked(index) = value;
        }

        private ref T ElementAtChecked(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((nuint)index, length);
            return ref ElementAtUnchecked((nuint)index);
        }
        private ref T ElementAtChecked(nuint index)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, length);
            return ref ElementAtUnchecked(index);
        }
        private ref T ElementAtUnchecked(nuint index)
        {
            Debug.Assert(index < Length);
            return ref head[index];
        }

        /// <summary>
        /// Initializes a new empty instance of the <see cref="NativeArray{T}"/> struct.
        /// </summary>
        private NativeArray(bool empty = false)
        {
            if (empty)
            {
                head = null;
                Length = 0;
#pragma warning disable S3971 // "GC.SuppressFinalize" should not be called
                GC.SuppressFinalize(this);
#pragma warning restore S3971 // "GC.SuppressFinalize" should not be called
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeArray{T}"/> struct.
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="alignmentExponent">The number of zeros to be at lowest significant bits.</param>
        /// <param name="fillWithZero">The internalValue which indicates whether the <see cref="NativeArray{T}"/> should perform <see cref="Clear"/> after allocating.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public NativeArray(nuint length, byte alignmentExponent = 0, bool fillWithZero = false)
        {
            var alignmentExponentLimit = (ushort)(Unsafe.SizeOf<nuint>() * 8);
            if (!NativeMemoryUtils.ValidateAllocationRequest<T>(length, out var lengthInBytes, alignmentExponent, alignmentExponentLimit))
            {
                NativeMemoryUtils.ThrowInvalidArguments<T>(length, alignmentExponent, alignmentExponentLimit);
#pragma warning disable S3971 // "GC.SuppressFinalize" should not be called
                GC.SuppressFinalize(this);
#pragma warning restore S3971 // "GC.SuppressFinalize" should not be called
                return;
            }
            this.alignmentExponent = alignmentExponent;
            head = (T*)NativeMemoryUtils.AllocateNativeMemoryInternal(lengthInBytes, alignmentExponent, true);
            Length = length;

            if (fillWithZero)
            {
                Clear();
            }
        }

        public static NativeArray<T> Create(ReadOnlySpan<T> values)
        {
            if (values.IsEmpty) return Empty;
            ArgumentOutOfRangeException.ThrowIfNegative(values.Length);
            return Create((ReadOnlyNativeSpan<T>)values);
        }

        public static NativeArray<T> Create(ReadOnlyNativeSpan<T> values)
        {
            if (values.IsEmpty) return Empty;
            var arr = new NativeArray<T>(values.Length);
            var ns = arr.NativeSpan;
            values.CopyTo(ns);
            return arr;
        }

        /// <summary>
        /// Clears the content of this <see cref="NativeArray{T}"/> object.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Clear() => NativeSpan.Clear();

        /// <inheritdoc cref="Span{T}.GetPinnableReference"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ref T GetPinnableReference() => ref *head;

        public MemoryHandle Pin(nuint elementIndex)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(elementIndex, Length);
            return new(head + elementIndex, default, this);
        }
        public MemoryHandle Pin(int elementIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
            return Pin((nuint)elementIndex);
        }
        public void Unpin() { }

        internal NativeArrayNativeMemoryManager<T> GetNativeSpanFactory() => new(this);

        public struct Enumerator : IEnumerator<T>
        {
            private NativeArray<T>? array;
            private nuint index;
            internal Enumerator(NativeArray<T> array)
            {
                index = 0;
                this.array = array;
            }

            public readonly T Current => array is { } ? array.ElementAtChecked(index) : default!;

            readonly object? IEnumerator.Current => Current;

            public void Dispose() => array = null;
            public bool MoveNext() => ++index < (array?.Length ?? 0);
            public void Reset() => index = 0;
        }

        public Enumerator GetEnumerator() => new(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<T>)this).GetEnumerator();
        NativeSpan<T> INativeSpanFactory<T>.GetNativeSpan() => NativeSpan;
        NativeSpan<T> INativeSpanFactory<T>.CreateNativeSpan(nuint start, nuint length) => NativeSpan.Slice(start, length);
        ReadOnlyNativeSpan<T> IReadOnlyNativeSpanFactory<T>.GetReadOnlyNativeSpan() => ReadOnlyNativeSpan;
        ReadOnlyNativeSpan<T> IReadOnlyNativeSpanFactory<T>.CreateReadOnlyNativeSpan(nuint start, nuint length) => ReadOnlyNativeSpan.Slice(start, length);
        ReadOnlyMemory<T> IReadOnlyNativeSpanFactory<T>.GetReadOnlyMemorySegment(nuint start) => throw new NotImplementedException();

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                var len = length;
                var h = head;
                var requestedAlignment = RequestedAlignment;
                if (h is not null && len > 0 && Interlocked.CompareExchange(ref length, 0, len) == len)
                {
                    NativeMemoryUtils.FreeInternal(h, len, requestedAlignment);
                    head = null;
                }
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'DisposeCore(bool disposing)' has code to free unmanaged resources
        // ~NativeArray()
        // {
        //     // Do not change this code. Put cleanup code in 'DisposeCore(bool disposing)' method
        //     DisposeCore(disposing: false);
        // }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
}
