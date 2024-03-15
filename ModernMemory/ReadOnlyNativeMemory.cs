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
    [StructLayout(LayoutKind.Sequential)]
    public readonly partial struct ReadOnlyNativeMemory<T>
    {
        private readonly ReadOnlyNativeSpanFactory nativeSpanFactory;
        private readonly nuint start;

        public nuint Length { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyNativeMemory(IReadOnlyNativeSpanFactory<T>? nativeSpanFactory, nuint start, nuint length)
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
            this = memory.IsEmpty ? default : new(new(memory), 0, (nuint)memory.Length);
        }

        /// <summary>
        /// Returns an empty <see cref="ReadOnlyNativeMemory{T}"/> object.
        /// </summary>
        public static ReadOnlyNativeMemory<T> Empty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => default;
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

        public ReadOnlySequence<T> AsReadOnlySequence(long maxElements = long.MaxValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxElements);
            var factory = nativeSpanFactory;
            if (factory.IsEmpty || maxElements == 0) return ReadOnlySequence<T>.Empty;
            if (factory.Length <= int.MaxValue || maxElements <= int.MaxValue)
            {
                var rom = factory.GetReadOnlyMemorySegment(0);
                if (rom.Length > maxElements) rom = rom.Slice((int)maxElements);
                return new(rom);
            }
            return CreateReadOnlySequence(factory, maxElements);
        }

        private static ReadOnlySequence<T> CreateReadOnlySequence(IReadOnlyNativeSpanFactory<T> factory, long maxElements)
        {
            maxElements = long.Min(maxElements, (long)factory.Length);
            long c = 0;
            var memory = factory.GetReadOnlyMemorySegment(0);
            if (memory.Length > maxElements - c) memory = memory.Slice(0, (int)(maxElements - c));
            var fs = new SimpleReadOnlySequenceSegment<T>(memory, 0);
            var es = fs;
            while (c < maxElements)
            {
                var s = factory.GetReadOnlyMemorySegment((nuint)c);
                if (s.Length > maxElements - c) s = s.Slice(0, (int)(maxElements - c));
                var ns = new SimpleReadOnlySequenceSegment<T>(s);
                es.SetNext(ns);
                es = ns;
                c += s.Length;
            }
            return new ReadOnlySequence<T>(fs, 0, es, es.Memory.Length);
        }

        #region Slice
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyNativeMemory<T> Slice(nuint start)
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
        public ReadOnlyNativeMemory<T> Slice(nuint start, nuint length)
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
    }
}
