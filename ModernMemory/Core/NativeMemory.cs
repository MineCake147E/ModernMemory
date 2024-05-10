using System;
using System.Buffers;
using System.Collections.Generic;
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
    /// <summary>
    /// Represents a contiguous region of memory.
    /// </summary>
    /// <typeparam name="T">The type of items in the <see cref="NativeMemory{T}"/>.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    [SuppressMessage("Major Code Smell", "S1168:Empty arrays and collections should be returned instead of null", Justification = "[] for NativeMemory is too slow")]
    public readonly partial struct NativeMemory<T> : IEquatable<NativeMemory<T>>, ISpanEnumerable<T>, IMemoryEnumerable<T>
    {
        private readonly NativeSpanFactory nativeSpanFactory;
        private readonly nuint start;
        private readonly nuint length;

        /// <summary>
        /// Gets the number of items in the current instance.
        /// </summary>
        /// <internalValue>The number of items in the current instance.</internalValue>
        public nuint Length => length;
        #region Constructors

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NativeMemory(NativeMemoryManager<T>? nativeSpanFactory, nuint start, nuint length)
        {
            if (length > 0 && nativeSpanFactory is not null)
            {
                this.nativeSpanFactory = new(nativeSpanFactory);
                this.start = start;
                this.length = length;
            }
            else
            {
                this = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NativeMemory(NativeSpanFactory nativeSpanFactory, nuint start, nuint length)
        {
            if (length > 0 && !nativeSpanFactory.IsEmpty)
            {
                this.nativeSpanFactory = nativeSpanFactory;
                this.start = start;
                this.length = length;
            }
            else
            {
                this = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory(Memory<T> memory)
        {
            this = memory.IsEmpty ? default : new(new NativeSpanFactory(memory), 0, (nuint)memory.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory(T[] array, int start, int length)
        {
            this = new(array.AsMemory(start, length));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory(T[] array, int start)
        {
            this = new(array.AsMemory(start));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory(T[] array)
        {
            this = new(array.AsMemory());
        }
        #endregion

        /// <summary>
        /// Returns an empty <see cref="NativeMemory{T}"/> object.
        /// </summary>
        public static NativeMemory<T> Empty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => default;
        }

        /// <inheritdoc cref="Memory{T}.IsEmpty"/>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Length == 0;
        }

        /// <summary>
        /// Returns a <see cref="NativeSpan{T}"/> from the current instance.
        /// </summary>
        /// <internalValue>A span created from the current <see cref="NativeSpan{T}"/> object.</internalValue>
        public NativeSpan<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => nativeSpanFactory.CreateNativeSpan(start, Length);
        }

        /// <summary>
        /// Creates a handle for the <see cref="NativeMemory{T}"/> object.
        /// </summary>
        /// <returns>A handle for the <see cref="NativeMemory{T}"/> object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe MemoryHandle Pin() => nativeSpanFactory.Pin(start);

        public Memory<T> GetHeadMemory() => nativeSpanFactory.GetHeadMemory();

        #region Slice
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory<T> Slice(nuint start)
        {
            var currentLength = Length;
            var ol = currentLength - start;
            return ol <= currentLength ? new(nativeSpanFactory, this.start + start, ol) : ThrowSliceExceptions(start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory<T> Slice(nuint start, nuint length) => MathUtils.IsRangeInRange(Length, start, length)
                ? new(nativeSpanFactory, this.start + start, length)
                : ThrowSliceExceptions(start, length);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private NativeMemory<T> ThrowSliceExceptions(nuint start)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private NativeMemory<T> ThrowSliceExceptions(nuint start, nuint length)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Length - start);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory<T> SliceByRange(nuint startInclusive, nuint endExclusive)
        {
            var newLength = checked(endExclusive - startInclusive);
            return Slice(startInclusive, newLength);
        }
        #endregion

        #region Copy
        /// <summary>
        /// Attempts to copy the current <see cref="NativeMemory{T}"/> to a destination <see cref="NativeSpan{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(NativeSpan<T> destination) => Span.TryCopyTo(destination);

        /// <summary>
        /// Attempts to copy the current <see cref="NativeMemory{T}"/> to a destination <see cref="NativeMemory{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(NativeMemory<T> destination) => Span.TryCopyTo(destination.Span);

        /// <summary>
        /// Copies the content of this <see cref="NativeMemory{T}"/> into a <paramref name="destination"/> <see cref="NativeSpan{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeSpan<T> destination) => Span.CopyTo(destination);

        /// <summary>
        /// Copies the content of this <see cref="NativeMemory{T}"/> into a <paramref name="destination"/> <see cref="NativeMemory{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeMemory<T> destination) => Span.CopyTo(destination.Span);

        #endregion
        #region Equality

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(NativeMemory<T> other)
            => nativeSpanFactory == other.nativeSpanFactory
                && start == other.start
                && Length == other.Length;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj) => obj is NativeMemory<T> memory && Equals(memory);
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(nativeSpanFactory, start, Length);
        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => new(Span);

        public ReadOnlyNativeMemory<T>.Enumerator GetMemoryEnumerator() => new(this);
        ReadOnlyNativeMemory<T>.Enumerator ITypedEnumerable<T, ReadOnlyNativeMemory<T>.Enumerator>.GetEnumerator() => GetMemoryEnumerator();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(NativeMemory<T> left, NativeMemory<T> right) => left.Equals(right);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(NativeMemory<T> left, NativeMemory<T> right) => !(left == right);
        #endregion

        #region Conversions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator NativeMemory<T>(Memory<T> memory) => new(memory);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyNativeMemory<T>(NativeMemory<T> nativeMemory)
            => new(new ReadOnlyNativeMemory<T>.ReadOnlyNativeSpanFactory(nativeMemory.nativeSpanFactory), nativeMemory.start, nativeMemory.Length);
        #endregion
    }
}
