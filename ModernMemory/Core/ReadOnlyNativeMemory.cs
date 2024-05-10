using System;
using System.Buffers;
using System.Collections;
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
    [StructLayout(LayoutKind.Sequential)]
    public readonly partial struct ReadOnlyNativeMemory<T> : ISpanEnumerable<T>, IMemoryEnumerable<T>
    {
        private readonly ReadOnlyNativeSpanFactory nativeSpanFactory;
        private readonly nuint start;

        public nuint Length { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyNativeMemory(NativeMemoryManager<T>? nativeSpanFactory, nuint start, nuint length)
        {
            if (length > 0 && nativeSpanFactory is not null)
            {
                this.nativeSpanFactory = new(nativeSpanFactory);
                this.start = start;
                Length = length;
            }
            else
            {
                this = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyNativeMemory(ReadOnlyNativeSpanFactory nativeSpanFactory, nuint start, nuint length)
        {
            if (length > 0 && !nativeSpanFactory.IsEmpty)
            {
                this.nativeSpanFactory = nativeSpanFactory;
                this.start = start;
                Length = length;
            }
            else
            {
                this = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyNativeMemory(ReadOnlyMemory<T> memory)
        {
            this = memory.IsEmpty ? default : new(new ReadOnlyNativeSpanFactory(memory), 0, (nuint)memory.Length);
        }

        /// <summary>
        /// Returns an empty <see cref="ReadOnlyNativeMemory{T}"/> object.
        /// </summary>
        public static ReadOnlyNativeMemory<T> Empty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable S1168 // Empty arrays and collections should be returned instead of null
            get => default;
#pragma warning restore S1168 // Empty arrays and collections should be returned instead of null
        }

        /// <returns><see langword="true"/> if the read-only rom region is empty (that is, its <see cref="Length"/> is 0); otherwise, <see langword="false"/>.</returns>
        /// <inheritdoc cref="ReadOnlyMemory{T}.IsEmpty"/>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Length == 0;
        }

        /// <summary>
        /// Gets a span from the rom region.
        /// </summary>
        public ReadOnlyNativeSpan<T> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsEmpty ? default : nativeSpanFactory.CreateReadOnlyNativeSpan(start, Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe MemoryHandle Pin() => nativeSpanFactory.Pin(start);

        public ReadOnlyMemory<T> GetHeadMemory() => nativeSpanFactory.GetHeadMemory();


        #region Slice
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyNativeMemory<T> Slice(nuint start)
        {
            var currentLength = Length;
            var ol = currentLength - start;
            return ol <= currentLength ? new(nativeSpanFactory, this.start + start, ol) : ThrowSliceExceptions(start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyNativeMemory<T> Slice(nuint start, nuint length) => MathUtils.IsRangeInRange(Length, start, length)
                ? new(nativeSpanFactory, this.start + start, length)
                : ThrowSliceExceptions(start, length);


        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private ReadOnlyNativeMemory<T> ThrowSliceExceptions(nuint start)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private ReadOnlyNativeMemory<T> ThrowSliceExceptions(nuint start, nuint length)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Length - start);
            throw new InvalidOperationException("Something went wrong!");
        }
        #endregion

        #region Copy
        /// <summary>
        /// Attempts to copy the current <see cref="ReadOnlyNativeMemory{T}"/> to a destination <see cref="NativeSpan{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(NativeSpan<T> destination) => Span.TryCopyTo(destination);

        /// <summary>
        /// Attempts to copy the current <see cref="ReadOnlyNativeMemory{T}"/> to a destination <see cref="NativeMemory{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(NativeMemory<T> destination) => Span.TryCopyTo(destination.Span);

        /// <summary>
        /// Copies the content of this <see cref="ReadOnlyNativeMemory{T}"/> into a <paramref name="destination"/> <see cref="NativeSpan{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeSpan<T> destination) => Span.CopyTo(destination);

        /// <summary>
        /// Copies the content of this <see cref="ReadOnlyNativeMemory{T}"/> into a <paramref name="destination"/> <see cref="NativeMemory{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeMemory<T> destination) => Span.CopyTo(destination.Span);
        #endregion

        #region Conversions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyNativeMemory<T>(ReadOnlyMemory<T> memory) => new(memory);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyNativeMemory<T>(Memory<T> memory) => new(memory);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyNativeMemory<T>(T[] memory) => new(memory);

        #endregion

        Enumerator ITypedEnumerable<T, Enumerator>.GetEnumerator() => new(this);
        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => new(Span);

        public struct Enumerator : IEnumerator<T>
        {
            private ReadOnlyNativeMemory<T> memory;
            private nuint index;
            internal Enumerator(ReadOnlyNativeMemory<T> memory)
            {
                index = ~(nuint)0;
                this.memory = memory;
            }

            public readonly T Current => memory is { } && index < memory.Span.Length ? memory.Span.ElementAtUnchecked(index) : default!;

            readonly object? IEnumerator.Current => Current;

            public void Dispose() => memory = default;
            [MemberNotNullWhen(true, nameof(Current))]
            public bool MoveNext() => ++index < memory.Length;
            public void Reset() => index = ~(nuint)0;
        }
    }
}
