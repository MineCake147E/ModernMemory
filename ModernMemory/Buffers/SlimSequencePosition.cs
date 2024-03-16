using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct SlimSequencePosition : ISequencePosition<SlimSequencePosition>, IComparable<SlimSequencePosition>, IComparisonOperators<SlimSequencePosition, SlimSequencePosition, bool>
    {
        private readonly nuint segmentPosition;
        private readonly nuint index;

        internal SlimSequencePosition(nuint segmentPosition, nuint index)
        {
            this.segmentPosition = segmentPosition;
            this.index = index;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public nuint SegmentPosition => segmentPosition;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public nuint Index => index;

        public int CompareTo(SlimSequencePosition other)
        {
            var res = segmentPosition.CompareToByComparisonOperators(other.segmentPosition);
            var r2 = index.CompareToByComparisonOperators(other.index);
            if (res == 0) res = r2;
            return res;
        }
        public override bool Equals(object? obj) => obj is SlimSequencePosition slim && Equals(slim);
        public bool Equals(SlimSequencePosition other) => segmentPosition == other.segmentPosition && index == other.index;
        public override int GetHashCode() => HashCode.Combine(segmentPosition, index);

        public static bool operator ==(SlimSequencePosition left, SlimSequencePosition right) => left.Equals(right);
        public static bool operator !=(SlimSequencePosition left, SlimSequencePosition right) => !(left == right);

        public static bool operator <(SlimSequencePosition left, SlimSequencePosition right)
            => left.segmentPosition < right.segmentPosition || left.index < right.index;

        public static bool operator <=(SlimSequencePosition left, SlimSequencePosition right)
            => left.segmentPosition <= right.segmentPosition || left.index <= right.index;

        public static bool operator >(SlimSequencePosition left, SlimSequencePosition right)
            => left.segmentPosition > right.segmentPosition || left.index > right.index;

        public static bool operator >=(SlimSequencePosition left, SlimSequencePosition right)
            => left.segmentPosition >= right.segmentPosition || left.index >= right.index;

        private string GetDebuggerDisplay() => $"{segmentPosition}[{index}]";
        public override string? ToString() => GetDebuggerDisplay();
    }
}
