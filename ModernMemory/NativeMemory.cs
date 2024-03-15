using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory
{
    /// <summary>
    /// Represents a contiguous region of memory.
    /// </summary>
    /// <typeparam name="T">The type of items in the <see cref="NativeMemory{T}"/>.</typeparam>
    [StructLayout(LayoutKind.Sequential)]
    public readonly partial struct NativeMemory<T> : IEquatable<NativeMemory<T>>
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
        internal NativeMemory(INativeSpanFactory<T>? nativeSpanFactory, nuint start, nuint length)
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
            this = memory.IsEmpty ? default : new(new(memory), 0, (nuint)memory.Length);
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
            get => nativeSpanFactory is { } factory ? factory.CreateNativeSpan(start, Length) : default;
        }

        /// <summary>
        /// Creates a handle for the <see cref="NativeMemory{T}"/> object.
        /// </summary>
        /// <returns>A handle for the <see cref="NativeMemory{T}"/> object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe MemoryHandle Pin() => nativeSpanFactory is { } factory ? factory.Pin(start) : default;

        #region Slice
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory<T> Slice(nuint start)
        {
            var currentLength = Length;
            var olen = currentLength - start;
            if (olen <= currentLength)
            {
                return new(nativeSpanFactory, this.start + start, olen);
            }
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, currentLength);
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory<T> Slice(nuint start, nuint length)
        {
            var currentLength = Length;
            if (MathUtils.IsRangeInRange(currentLength, start, length))
            {
                return new(nativeSpanFactory, this.start + start, length);
            }
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, currentLength);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, currentLength - start);
            return default;
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
            => new(nativeMemory.nativeSpanFactory, nativeMemory.start, nativeMemory.Length);

        #endregion
    }
}
