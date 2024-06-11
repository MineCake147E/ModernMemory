using System.Collections;

namespace ModernMemory.Tests
{
    public readonly struct SliceData : IEnumerable<nuint>, ISliceable<SliceData, nuint>, IEquatable<SliceData>
    {
        public SliceData(nuint start)
        {
            Start = start;
            SliceByLength = false;
        }

        public SliceData(nuint start, nuint length)
        {
            Start = start;
            Length = length;
            SliceByLength = true;
        }

        public nuint Start { get; }
        public nuint Length { get; }
        public bool SliceByLength { get; }

        public Enumerator GetEnumerator() => new(this);

        IEnumerator<nuint> IEnumerable<nuint>.GetEnumerator() => GetEnumerator();
        public override string ToString() => SliceByLength ? $"<Start: {Start}, Length: {Length}>" : $"<Start: {Start}>";
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public SliceData Slice(nuint start)
        {
            var newStart = start + Start;
            var length = Length;
            if (!SliceByLength) return new(newStart);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(start, length);
            return new(newStart, length - start);
        }
        public SliceData Slice(nuint start, nuint length)
        {
            var newStart = start + Start;
            var currentLength = Length;
            if (!SliceByLength) return new(newStart, length);
            if (!MathUtils.IsRangeInRange(currentLength, start, length))
            {
                NativeMemoryCore.ThrowSliceExceptions(start, length, currentLength);
            }
            return new(newStart, length);
        }

        public override bool Equals(object? obj) => obj is SliceData data && Equals(data);
        public bool Equals(SliceData other) => Start.Equals(other.Start) && Length.Equals(other.Length) && SliceByLength == other.SliceByLength;
        public override int GetHashCode() => HashCode.Combine(Start, Length, SliceByLength);

        public struct Enumerator(SliceData slice) : IEnumerator<nuint>
        {
            private readonly nuint start = slice.Start;
            private readonly nuint length = slice.Length;
            private nuint counter = 0;

            public readonly nuint Current => start + counter;

            readonly object IEnumerator.Current => Current;

            public readonly void Dispose() { }
            public bool MoveNext() => ++counter < length;
            public void Reset() => counter = 0;
        }

        public static bool operator ==(SliceData left, SliceData right) => left.Equals(right);
        public static bool operator !=(SliceData left, SliceData right) => !(left == right);
    }
}
