using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections;

namespace ModernMemory.Buffers
{
    public interface IReadOnlySequence<T, TSelf, TSequencePosition, TEnumerator> : ITypedEnumerable<T, TEnumerator>, ISliceable<TSelf, nuint>
        where TSelf : struct, IReadOnlySequence<T, TSelf, TSequencePosition, TEnumerator>
        where TSequencePosition : unmanaged, ISequencePosition<TSequencePosition>
        where TEnumerator : IEnumerator<T>
    {
        static virtual TSelf Empty => default;

        /// <inheritdoc cref="ReadOnlySequence{T}.IsEmpty"/>
        bool IsEmpty { get; }
        /// <inheritdoc cref="ReadOnlySequence{T}.IsSingleSegment"/>
        bool IsSingleSegment { get; }
        /// <inheritdoc cref="ReadOnlySequence{T}.Length"/>
        nuint Length { get; }
        /// <inheritdoc cref="ReadOnlySequence{T}.Start"/>
        TSequencePosition Start { get; }
        /// <inheritdoc cref="ReadOnlySequence{T}.End"/>
        TSequencePosition End { get; }
        /// <inheritdoc cref="ReadOnlySequence{T}.First"/>
        ReadOnlyNativeMemory<T> First { get; }
        /// <inheritdoc cref="ReadOnlySequence{T}.FirstSpan"/>
        ReadOnlyNativeSpan<T> FirstSpan { get; }

        /// <inheritdoc cref="ReadOnlySequence{T}.GetOffset(SequencePosition)"/>
        nuint GetOffset(TSequencePosition position);

        /// <inheritdoc cref="ReadOnlySequence{T}.GetPosition(long)"/>
        TSequencePosition GetPosition(nuint offset);
        /// <inheritdoc cref="ReadOnlySequence{T}.GetPosition(long, SequencePosition)"/>
        TSequencePosition GetPosition(nuint offset, TSequencePosition origin);

        TSelf Slice(TSequencePosition start);
        TSelf Slice(nuint start, TSequencePosition end);
        TSelf Slice(TSequencePosition start, nuint length);
        TSelf Slice(TSequencePosition start, TSequencePosition end);

        /// <inheritdoc cref="ReadOnlySequence{T}.TryGet(ref SequencePosition, out ReadOnlyMemory{T}, bool)"/>
        bool TryGet(TSequencePosition position, out ReadOnlyNativeMemory<T> memory, out TSequencePosition newPosition);

        static abstract implicit operator ReadOnlySequenceSlim<T>(TSelf self);
    }
}
