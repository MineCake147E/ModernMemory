using System;
using System.Runtime.InteropServices;

namespace ModernMemory.Buffers
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ReadOnlySequenceSegment<T> : IRangeComparable<nuint>
    {
        public nuint RunningIndex { get; }

        public ReadOnlyNativeMemory<T> Memory { get; }

        public ReadOnlySequenceSegment(ReadOnlyNativeMemory<T> memory)
        {
            Memory = memory;
        }

        internal ReadOnlySequenceSegment(ReadOnlyNativeMemory<T> memory, nuint runningIndex)
        {
            Memory = memory;
            RunningIndex = runningIndex;
        }

        internal ReadOnlySequenceSegment(ReadOnlyNativeMemory<T> memory, ReadOnlySequenceSegment<T> previousSegment)
        {
            Memory = memory;
            RunningIndex = previousSegment.RunningIndex + previousSegment.Memory.Length;
        }

        public int CompareTo(nuint other)
        {
            var runningIndex = RunningIndex;
            var n = other >= runningIndex + Memory.Length;
            var p = other < runningIndex;
            return (p ? 1 : 0) - (n ? 1 : 0);
        }
    }
}
