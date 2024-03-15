using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    internal sealed class SimpleReadOnlySequenceSegment<T> : System.Buffers.ReadOnlySequenceSegment<T>
    {
        public SimpleReadOnlySequenceSegment(ReadOnlyMemory<T> memory, System.Buffers.ReadOnlySequenceSegment<T> next, long runningIndex)
        {
            Memory = memory;
            Next = next;
            RunningIndex = runningIndex;
        }
        public SimpleReadOnlySequenceSegment(ReadOnlyMemory<T> memory, long runningIndex)
        {
            Memory = memory;
            RunningIndex = runningIndex;
        }
        public SimpleReadOnlySequenceSegment(ReadOnlyMemory<T> memory)
        {
            Memory = memory;
        }
        internal void SetNext(SimpleReadOnlySequenceSegment<T> next)
        {
            Next = next;
            next.RunningIndex = RunningIndex + Memory.Length;
        }
    }
}
