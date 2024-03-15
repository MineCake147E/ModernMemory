using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers.DataFlow
{
    public interface IBufferedDataProvider<T> : IDataProvider<T>
    {
        /// <summary>
        /// Returns a <see cref="ReadOnlyNativeSpan{T}"/> to read elements from.<br/>
        /// </summary>
        /// <returns>A <see cref="ReadOnlyNativeSpan{T}"/> which points to the internal reading buffer.</returns>
        ReadOnlyNativeSpan<T> CurrentSpan { get; }

        /// <summary>
        /// Returns a <see cref="ReadOnlyNativeMemory{T}"/> to read elements from.<br/>
        /// </summary>
        /// <returns>A <see cref="ReadOnlyNativeMemory{T}"/> which points to the internal reading buffer.</returns>
        ReadOnlyNativeMemory<T> CurrentMemory { get; }

        /// <summary>
        /// Returns a sequence of <see cref="ReadOnlyNativeMemory{T}"/> to read elements from.<br/>
        /// </summary>
        /// <param name="minimumSize">The number of elements desired to be read.</param>
        /// <returns>A sequence of <see cref="ReadOnlyNativeMemory{T}"/> which points to the internal reading buffer.</returns>
        ReadOnlyNativeSpan<ReadOnlyNativeMemory<T>> GetBufferedSequence(nuint minimumSize = 0);

        /// <summary>
        /// Returns a sequence of <see cref="ReadOnlyNativeMemory{T}"/> to read elements from.<br/>
        /// </summary>
        /// <param name="minimumSize">The number of elements desired to be read.</param>
        /// <returns>A sequence of <see cref="ReadOnlyNativeMemory{T}"/> which points to the internal reading buffer.</returns>
        ValueTask<ReadOnlyNativeMemory<ReadOnlyNativeMemory<T>>> GetBufferedSequenceAsync(nuint minimumSize = 0, CancellationToken cancellationToken = default);
    }
}
