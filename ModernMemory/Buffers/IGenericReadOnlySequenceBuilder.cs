using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModernMemory.Collections;
using ModernMemory.DataFlow;

namespace ModernMemory.Buffers
{
    /// <summary>
    /// Designed for use in implementations of <see cref="ISequenceDataReader{T}"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TSequence"></typeparam>
    /// <typeparam name="TSequencePosition"></typeparam>
    /// <typeparam name="TEnumerator"></typeparam>
    public interface IGenericReadOnlySequenceBuilder<T, TSequence, TSequencePosition, TEnumerator> : IDisposable
        where TSequence : struct, ISliceableReadOnlySequence<T, TSequence, TSequencePosition, TEnumerator>
        where TSequencePosition : struct, ISequencePosition<TSequencePosition>
        where TEnumerator : IEnumerator<T>
    {
        TSequence Build();

        nuint CurrentElementCount { get; }

        nuint CurrentSegmentCount { get; }

        void Clear();

        void AdvanceTo(TSequencePosition consumed);

        nuint AdvanceTo(TSequencePosition consumed, TSequencePosition examined);

        nuint Append(ReadOnlyNativeMemory<T> memory);

        nuint Append(ReadOnlyNativeSpan<ReadOnlyNativeMemory<T>> memories);
    }

    /// <summary>
    /// Designed for use in implementations of <see cref="ISequenceDataReader{T}"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReadOnlyMemorySequenceBuilder<T> : IGenericReadOnlySequenceBuilder<T, ReadOnlyMemorySequence<T>, SlimSequencePosition, ReadOnlyMemorySequence<T>.Enumerator>
    {
        ReadOnlySequenceSlim<T> BuildSlim();
    }
}
