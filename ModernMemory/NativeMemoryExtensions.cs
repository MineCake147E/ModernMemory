using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Buffers.DataFlow;

namespace ModernMemory
{
    public static partial class NativeMemoryExtensions
    {
        #region AsNativeSpan
        #region Span
        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> over the target <paramref name="span"/>.
        /// </summary>
        /// <typeparam name="T">The type of the span.</typeparam>
        /// <param name="span">The <see cref="Span{T}"/> to convert.</param>
        /// <returns>The <see cref="NativeSpan{T}"/> representation of the <paramref name="span"/>.</returns>
        public static NativeSpan<T> AsNativeSpan<T>(this Span<T> span) => new(span);
        #endregion

        #region Array
        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> over the target <paramref name="array"/>.
        /// </summary>
        /// <typeparam name="T">The type of the memory.</typeparam>
        /// <param name="array">The <see cref="Array"/> to convert.</param>
        /// <returns>The <see cref="NativeSpan{T}"/> representation of the memory.</returns>
        public static NativeSpan<T> AsNativeSpan<T>(this T[]? array) => new(array);

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> over the portion of the target <paramref name="array"/> beginning at a specified position for a specified length.
        /// </summary>
        /// <typeparam name="T">The type of the memory.</typeparam>
        /// <param name="array">The target memory.</param>
        /// <param name="start">The index at which to begin the span.</param>
        /// <returns>The <see cref="NativeSpan{T}"/> representation of the memory.</returns>
        public static NativeSpan<T> AsNativeSpan<T>(this T[]? array, int start) => new(array, (nuint)start);

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> over the portion of the target <paramref name="array"/> beginning at a specified position for a specified length.
        /// </summary>
        /// <typeparam name="T">The type of the memory.</typeparam>
        /// <param name="array">The target memory.</param>
        /// <param name="start">The index at which to begin the span.</param>
        /// <param name="length">The number of items in the span.</param>
        /// <returns>The <see cref="NativeSpan{T}"/> representation of the memory.</returns>
        public static NativeSpan<T> AsNativeSpan<T>(this T[]? array, int start, int length) => new(array, (nuint)start, (nuint)length);

        #endregion

        #region ArraySegment
        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> over the target <paramref name="segment"/>.
        /// </summary>
        /// <typeparam name="T">The type of the memory segment.</typeparam>
        /// <param name="segment">The target memory segment.</param>
        /// <returns>The <see cref="NativeSpan{T}"/> representation of the memory.</returns>
        public static NativeSpan<T> AsNativeSpan<T>(this ArraySegment<T> segment) => new(segment);

        #endregion

        #region NativeArray
        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> over the target <paramref name="array"/>.
        /// </summary>
        /// <typeparam name="T">The type of the memory.</typeparam>
        /// <param name="array">The memory to convert.</param>
        /// <returns>The <see cref="NativeSpan{T}"/> representation of the memory.</returns>
        public static NativeSpan<T> AsNativeSpan<T>(this NativeArray<T> array) where T : unmanaged
            => array.NativeSpan;

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> over the portion of the target <paramref name="array"/> beginning at a specified position for a specified length.
        /// </summary>
        /// <typeparam name="T">The type of the memory.</typeparam>
        /// <param name="array">The target memory.</param>
        /// <param name="start">The index at which to begin the span.</param>
        /// <param name="length">The number of items in the span.</param>
        /// <returns>The <see cref="NativeSpan{T}"/> representation of the memory.</returns>
        public static NativeSpan<T> AsNativeSpan<T>(this NativeArray<T> array, nuint start, nuint length) where T : unmanaged
            => array.NativeSpan.Slice(start, length);
        #endregion

        #region String

        public static ReadOnlyNativeSpan<char> AsNativeSpan(this string text) => new(text.AsSpan());

        public static ReadOnlyNativeSpan<char> AsNativeSpan(this string text, Index startIndex) => new(text.AsSpan(startIndex));

        public static ReadOnlyNativeSpan<char> AsNativeSpan(this string text, int start, int length) => new(text.AsSpan(start, length));

        public static ReadOnlyNativeSpan<char> AsNativeSpan(this string text, Range range) => new(text.AsSpan(range));
        #endregion

        #endregion

        #region AsNativeMemory

        #region String

        public static ReadOnlyNativeMemory<char> AsNativeMemory(this string text) => new(text.AsMemory());

        public static ReadOnlyNativeMemory<char> AsNativeMemory(this string text, Index startIndex) => new(text.AsMemory(startIndex));

        public static ReadOnlyNativeMemory<char> AsNativeMemory(this string text, int start, int length) => new(text.AsMemory(start, length));

        public static ReadOnlyNativeMemory<char> AsNativeMemory(this string text, Range range) => new(text.AsMemory(range));
        #endregion

        #region NativeArray
        public static NativeMemory<T> AsNativeMemory<T>(this NativeArray<T> array)
            => new(array.GetNativeSpanFactory(), 0, array.Length);

        public static NativeMemory<T> AsNativeMemory<T>(this NativeArray<T> array, nuint start)
            => new(array.GetNativeSpanFactory(), start, array.Length - start);

        public static NativeMemory<T> AsNativeMemory<T>(this NativeArray<T> array, nuint start, nuint length)
            => new(array.GetNativeSpanFactory(), start, length);
        #endregion

        #endregion

        #region CopyTo

        public static void CopyTo<T>(this ReadOnlySpan<T> span, NativeSpan<T> destination) => new ReadOnlyNativeSpan<T>(span).CopyTo(destination);
        public static bool TryCopyTo<T>(this ReadOnlySpan<T> span, NativeSpan<T> destination) => new ReadOnlyNativeSpan<T>(span).TryCopyTo(destination);

        public static nuint CopyAtMostTo<T>(this ReadOnlySpan<T> span, NativeSpan<T> destination, nuint offset = 0) => new ReadOnlyNativeSpan<T>(span).CopyAtMostTo(destination, offset);
        public static nuint CopyAtMostTo<T>(this Span<T> span, NativeSpan<T> destination, nuint offset = 0) => new ReadOnlyNativeSpan<T>(span).CopyAtMostTo(destination, offset);
        public static nuint CopyAtMostTo<T>(this NativeSpan<T> span, NativeSpan<T> destination, nuint offset = 0) => new ReadOnlyNativeSpan<T>(span).CopyAtMostTo(destination, offset);

        public static int CopyAtMostTo<T>(this ReadOnlySpan<T> span, Span<T> destination, int offset = 0) => new ReadOnlyNativeSpan<T>(span).CopyAtMostTo(destination, offset);
        public static int CopyAtMostTo<T>(this Span<T> span, Span<T> destination, int offset = 0) => new ReadOnlyNativeSpan<T>(span).CopyAtMostTo(destination, offset);
        public static int CopyAtMostTo<T>(this NativeSpan<T> span, Span<T> destination, int offset = 0) => new ReadOnlyNativeSpan<T>(span).CopyAtMostTo(destination, offset);
        #endregion

        #region WriteTo
        public static void WriteTo<T, TBufferWriter>(this ReadOnlySequence<T> sequence, ref TBufferWriter bufferWriter) where TBufferWriter : IBufferWriter<T>
        {
            using var dw = DataWriter<T>.CreateFrom(ref bufferWriter);
            dw.WriteAtMost(sequence);
        }

        public static SequencePosition WriteAtMostTo<T>(this ReadOnlySequence<T> sequence, NativeSpan<T> destination)
        {
            using var dw = DataWriter.CreateFrom(destination);
            return dw.WriteAtMost(sequence);
        }

        #endregion

        #region AsReadOnlySequence

        #endregion

        public static INativeMemoryOwner<T> AsNativeMemoryOwner<T>(this IMemoryOwner<T> owner) => new MemoryOwnerWrapper<T>(owner);

        public static NativeMemoryManager<T> AsNativeMemoryManager<T>(this MemoryManager<T> memoryManager) => new MemoryManagerWrapper<T>(memoryManager);

    }
}
