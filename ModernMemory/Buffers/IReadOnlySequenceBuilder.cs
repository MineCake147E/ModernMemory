using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers.DataFlow;

namespace ModernMemory.Buffers
{
    /// <summary>
    /// Designed for use in implementations of <see cref="ISequenceDataReader{T}"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="TSequence"></typeparam>
    /// <typeparam name="TSequencePosition"></typeparam>
    /// <typeparam name="TEnumerator"></typeparam>
    public interface IReadOnlySequenceBuilder<T, TSequence, TSequencePosition, TEnumerator> : IDisposable
        where TSequence : struct, IReadOnlySequence<T, TSequence, TSequencePosition, TEnumerator>
        where TSequencePosition : unmanaged, ISequencePosition<TSequencePosition>
        where TEnumerator : IEnumerator<T>
    {
        TSequence Build();

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
    public interface IReadOnlySequenceSlimBuilder<T> : IDisposable
    {
        ReadOnlySequenceSlim<T> Build();

        nuint CurrentLength { get; }

        void Clear();

        nuint AdvanceTo(SlimSequencePosition consumed);

        nuint AdvanceTo(SlimSequencePosition consumed, SlimSequencePosition examined);

        nuint Append(ReadOnlyNativeMemory<T> segment);

        nuint Append(ReadOnlyNativeSpan<ReadOnlyNativeMemory<T>> segments);
    }
}
