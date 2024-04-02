using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModernMemory.Collections;

namespace ModernMemory.Buffers.DataFlow
{
    public interface INativeBufferWriter<T> : IBufferWriter<T>
    {
        /// <summary>
        /// Gets the maximum buffer size for <see cref="GetNativeMemory(nuint)"/>.<br/>
        /// </summary>
        /// <param name="space">When this method returns, contains the maximum buffer size for <see cref="GetNativeMemory(nuint)"/>, if the <see cref="INativeBufferWriter{T}"/> can suggest it; otherwise, the <see cref="nuint.MaxValue"/>.<br/>
        /// This parameter is passed uninitialized.</param>
        /// <returns><see langword="true"/> if the <see cref="INativeBufferWriter{T}"/> can suggest the maximum buffer size; otherwise, <see langword="false"/>.</returns>
        bool TryGetMaxBufferSize(out nuint space);

        /// <summary>
        /// Notifies <see cref="INativeBufferWriter{T}"/> that <paramref name="count"/> amount of data was written to the output <see cref="NativeSpan{T}"/>/<see cref="NativeMemory{T}"/>
        /// </summary>
        /// <param name="count">The number of data items written to the <see cref="NativeSpan{T}"/> or <see cref="NativeMemory{T}"/>.</param>
        /// <remarks>You must request a new buffer after calling <see cref="Advance(nuint)"/> to continue writing more data and cannot write to a previously acquired buffer.</remarks>
        void Advance(nuint count);

        void IBufferWriter<T>.Advance(int count)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            Advance((nuint)count);
        }

        #region Get*
        /// <summary>
        /// Returns a <see cref="NativeSpan{T}"/> to write to that is at least the requested size (specified by <paramref name="sizeHint"/>).<br/>
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned <see cref="NativeSpan{T}"/>.</param>
        /// <remarks>
        /// This must never return an empty <see cref="NativeSpan{T}"/> but it can throw
        /// if the requested buffer size is not available.<br/>
        /// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.<br/>
        /// You must request a new buffer after calling <see cref="Advance(nuint)"/> to continue writing more data and cannot write to a previously acquired buffer.
        /// </remarks>
        /// <returns>The <see cref="NativeSpan{T}"/> for the buffer.</returns>
        NativeSpan<T> GetNativeSpan(nuint sizeHint = 0);

        /// <summary>
        /// Returns a <see cref="NativeMemory{T}"/> to write to that is at least the requested size (specified by <paramref name="sizeHint"/>).<br/>
        /// </summary>
        /// <remarks>
        /// This must never return an empty <see cref="NativeSpan{T}"/> but it can throw
        /// if the requested buffer size is not available.<br/>
        /// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.<br/>
        /// You must request a new buffer after calling <see cref="Advance(nuint)"/> to continue writing more data and cannot write to a previously acquired buffer.
        /// </remarks>
        /// <param name="sizeHint">The minimum length of the returned <see cref="NativeSpan{T}"/>.</param>
        /// <returns>The <see cref="NativeMemory{T}"/> for the buffer.</returns>
        NativeMemory<T> GetNativeMemory(nuint sizeHint = 0);
        #endregion

        #region TryGet*
        /// <summary>
        /// Returns a <see cref="NativeSpan{T}"/> to write to that is at least the requested size (specified by <paramref name="sizeHint"/>), or the maximum size that this <see cref="INativeBufferWriter{T}"/> can offer, whichever smaller.<br/>
        /// </summary>
        /// <param name="sizeHint">The minimum length of the returned <see cref="NativeSpan{T}"/>.</param>
        /// <remarks>
        /// This can return an empty <see cref="NativeSpan{T}"/> but it can not throw
        /// if no buffer is available.<br/>
        /// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.<br/>
        /// You must request a new buffer after calling <see cref="Advance(nuint)"/> to continue writing more data and cannot write to a previously acquired buffer.
        /// </remarks>
        /// <returns>The <see cref="NativeSpan{T}"/> for the buffer.</returns>
        NativeSpan<T> TryGetNativeSpan(nuint sizeHint = 0);

        /// <summary>
        /// Returns a <see cref="NativeMemory{T}"/> to write to that is at least the requested size (specified by <paramref name="sizeHint"/>), or the maximum size that this <see cref="INativeBufferWriter{T}"/> can offer, whichever smaller.<br/>
        /// </summary>
        /// <remarks>
        /// This can return an empty <see cref="NativeSpan{T}"/> but it can not throw
        /// if no buffer is available.<br/>
        /// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.<br/>
        /// You must request a new buffer after calling <see cref="Advance(nuint)"/> to continue writing more data and cannot write to a previously acquired buffer.
        /// </remarks>
        /// <param name="sizeHint">The minimum length of the returned <see cref="NativeSpan{T}"/>.</param>
        /// <returns>The <see cref="NativeMemory{T}"/> for the buffer.</returns>
        NativeMemory<T> TryGetNativeMemory(nuint sizeHint = 0);
        #endregion

        #region Write
        void Write(ReadOnlySpan<T> values) => Write(new ReadOnlyNativeSpan<T>(values));
        void Write(ReadOnlyNativeSpan<T> values)
        {
            if (values.IsEmpty) return;
            var span = TryGetNativeSpan();
            if (values.TryCopyTo(span))
            {
                Advance(values.Length);
                return;
            }
            var vs = values;
            while (!vs.IsEmpty)
            {
                span = TryGetNativeSpan(vs.Length);
                if (vs.TryCopyTo(span))
                {
                    Advance(vs.Length);
                    return;
                }
                if (span.IsEmpty)
                {
                    throw new ArgumentException($"Ran out of buffer!", nameof(values));
                }
                vs.Slice(0, span.Length).CopyTo(span);
                Advance(span.Length);
                vs = vs.Slice(span.Length);
            }
        }

        void WriteItems<TEnumerable>(TEnumerable items) where TEnumerable : IEnumerable<T>
        {
            nuint allocSize = 0;
            nuint itemsWritten = 0;
            NativeSpan<T> span = default;
            foreach (var item in items)
            {
                while (itemsWritten >= span.Length)
                {
                    Advance(itemsWritten);
                    span = GetNativeSpan(allocSize);
                    if (span.IsEmpty)
                    {
                        throw new ArgumentException($"Ran out of buffer!", nameof(items));
                    }
                }
                span[itemsWritten] = item;
                itemsWritten++;
            }
            Advance(itemsWritten);
        }
        #endregion
    }
}
