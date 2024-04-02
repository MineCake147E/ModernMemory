using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers.DataFlow;
using ModernMemory.Collections;

namespace ModernMemory.Buffers
{
    public ref struct SequenceReader<T, TSequence, TSequencePosition, TEnumerator>
        where TSequence : struct, IReadOnlySequence<T, TSequence, TSequencePosition, TEnumerator>
        where TSequencePosition : struct, ISequencePosition<TSequencePosition>
        where TEnumerator : IEnumerator<T>
    {
        private ReadOnlyNativeSpan<T> buffer;
        private TSequence sequence;
        private TSequencePosition copied;
        private MemoryResizer<T> resizer;
        private nuint totalElementsRead = 0;

        public SequenceReader(TSequence sequence)
        {
            this.sequence = sequence;
            buffer = default;
            copied = sequence.Start;
            TryGetNativeSpan(1);
        }

        public SequenceReader(TSequence sequence, nuint initialReadCapacity)
        {
            this.sequence = sequence;
            resizer = new(initialReadCapacity);
            buffer = default;
            copied = sequence.Start;
            TryGetNativeSpan(initialReadCapacity);
        }

        public readonly nuint TotalElementsRead => totalElementsRead;

        public ReadOnlyNativeSpan<T> TryGetNativeSpan(nuint size)
        {
            var res = buffer;
            if (res.Length < size)
            {
                var remained = res.Length;
                var cp = copied;
                var s2 = sequence.GetSegmentAlignedLength(size - res.Length, cp, out var segments);
                if (res.IsEmpty && segments <= 1)
                {
                    var f = sequence.TryGet(cp, out var m, out var p);
                    Debug.Assert(f);
                    buffer = res = m.Span;
                    copied = p;
                }
                else
                {
                    MemoryResizer<T>.LazyInit(ref resizer, s2, res);
                    var span = resizer.NativeMemory.Span;
                    var dw = span.Slice(remained).AsDataWriter();
                    var pos = cp;
                    while (!dw.IsCompleted && sequence.TryGet(pos, out var m, out var np))
                    {
                        var h = dw.TryGetRemainingElementsToWrite(out var g);
                        if (!h || g < m.Length) break;
                        var written = dw.WriteAtMost(m.Span);
                        Debug.Assert(written == m.Length);
                        pos = np;
                    }
                    var l = dw.GetElementsWritten();
                    dw.Dispose();
                    buffer = res = span.Slice(0, remained + l);
                    copied = pos;
                }
            }
            return res;
        }

        public void Advance(nuint count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(count, buffer.Length);
            totalElementsRead += count;
            buffer = buffer.Slice(count);
        }

        public void Dispose()
        {
            resizer.Dispose();
            resizer = default;
            sequence = default;
        }
    }
}
