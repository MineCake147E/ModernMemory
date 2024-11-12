using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections;

namespace ModernMemory.Buffers
{
    public interface ISliceableReadOnlySequence<T, TSelf, TSequencePosition, TEnumerator> : IReadOnlySequence<T, TSequencePosition>, ITypedEnumerable<T, TEnumerator>, ISliceable<TSelf, nuint>
        where TSelf : struct, ISliceableReadOnlySequence<T, TSelf, TSequencePosition, TEnumerator>
        where TSequencePosition : struct, ISequencePosition<TSequencePosition>
        where TEnumerator : IEnumerator<T>
    {
        static virtual TSelf Empty => default;

        TSelf Slice(TSequencePosition start);
        TSelf Slice(nuint start, TSequencePosition end) => Slice(((TSelf)this).GetPosition(start), end);
        TSelf Slice(TSequencePosition start, nuint length) => Slice(start, ((TSelf)this).GetPosition(length, start));
        TSelf Slice(TSequencePosition start, TSequencePosition end);

        static abstract implicit operator ReadOnlySequenceSlim<T>(TSelf self);

        ReadOnlySequenceSlim<T> IReadOnlySequence<T, TSequencePosition>.AsReadOnlySequenceSlim() => (ReadOnlySequenceSlim<T>)(TSelf)this;
    }
}
