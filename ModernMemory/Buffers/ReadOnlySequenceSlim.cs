using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections;

namespace ModernMemory.Buffers
{
    public readonly ref struct ReadOnlySequenceSlim<T>
    {
        private readonly ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>> segments;
        private readonly nuint firstIndex;
        private readonly nuint endIndex;
        private readonly nuint firstRunningIndex;
        private readonly nuint lastRunningIndex;

        [SkipLocalsInit]
        public ReadOnlySequenceSlim(ReadOnlyNativeMemory<T> memory)
        {
            var segment = new ReadOnlySequenceSegment<T>(memory, 0);
            var array = new ReadOnlySequenceSegment<T>[] { segment };
            this = new(array);
        }

        [SkipLocalsInit]
        public ReadOnlySequenceSlim(ReadOnlySpan<ReadOnlyNativeMemory<T>> segments)
        {
            var array = new ReadOnlySequenceSegment<T>[segments.Length];
            var span = array.AsSpan(0, segments.Length);
            nuint runningIndex = 0;
            for (var i = 0; i < span.Length; i++)
            {
                var segment = segments[i];
                span[i] = new(segment, runningIndex);
                runningIndex += segment.Length;
            }
            this = new(array);
        }

        [SkipLocalsInit]
        public ReadOnlySequenceSlim(ReadOnlySpan<T[]> segments)
        {
            var array = new ReadOnlySequenceSegment<T>[segments.Length];
            var span = array.AsSpan(0, segments.Length);
            nuint runningIndex = 0;
            for (var i = 0; i < span.Length; i++)
            {
                var segment = segments[i].AsMemory();
                span[i] = new(segment, runningIndex);
                runningIndex += (nuint)segment.Length;
            }
            this = new(array);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [SkipLocalsInit]
        public ReadOnlySequenceSlim(ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>> segments)
        {
            var span = segments;
            if (span.IsEmpty)
            {
                this = default;
                return;
            }
            var head = span.Head;
            var tail = span.Tail;
            var headRunningIndex = head.RunningIndex;
            var tailRunningIndex = tail.RunningIndex;
            ArgumentOutOfRangeException.ThrowIfLessThan(tailRunningIndex, headRunningIndex);
            this.segments = segments;
            firstIndex = 0;
            endIndex = tail.Memory.Length;
            firstRunningIndex = headRunningIndex;
            lastRunningIndex = tailRunningIndex;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [SkipLocalsInit]
        public ReadOnlySequenceSlim(ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>> segments, nuint firstIndex, nuint endIndex)
        {
            var span = segments;
            if (span.IsEmpty)
            {
                this = default;
                return;
            }
            var head = span.Head;
            var tail = span.Tail;
            var headRunningIndex = head.RunningIndex;
            var tailRunningIndex = tail.RunningIndex;
            ArgumentOutOfRangeException.ThrowIfLessThan(tailRunningIndex, headRunningIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(firstIndex, head.Memory.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(endIndex, tail.Memory.Length);
            var last = tailRunningIndex + endIndex;
            var first = headRunningIndex + firstIndex;
            ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
            if (last == first)  // Empty
            {
                this = default;
                return;
            }
            this.segments = segments;
            this.firstIndex = firstIndex;
            this.endIndex = endIndex;
            firstRunningIndex = headRunningIndex;
            lastRunningIndex = tailRunningIndex;
        }

        public static ReadOnlySequenceSlim<T> Empty => default;

        public nuint Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => segments.IsEmpty ? 0 : lastRunningIndex + endIndex - (firstRunningIndex + firstIndex);
        }

        public T this[nuint index]
        {
            get
            {
                var pos = GetPosition(index);
                var span = segments[pos.SegmentPosition].Memory.Span;
                var i = pos.Index;
                Debug.Assert(i < span.Length);
                return span.ElementAtUnchecked(i);
            }
        }

        public bool IsEmpty => Length == 0;
        public bool IsSingleSegment => segments.Length == 1;
        public SequencePositionSlim Start => IsEmpty ? default : new(0, firstIndex);
        public SequencePositionSlim End => IsEmpty ? default : new(segments.Length - 1, endIndex);
        public ReadOnlyNativeMemory<T> First => segments.IsEmpty ? default : segments[0].Memory;
        public ReadOnlyNativeSpan<T> FirstSpan => First.Span;


        public nuint GetOffset(SequencePositionSlim position)
        {
            AssertPosition(position, out _, out var offset);
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private bool VerifyPosition(SequencePositionSlim position, out ReadOnlySequenceSegment<T> segment, out nuint positionInSequence)
        {
            Unsafe.SkipInit(out segment);
            Unsafe.SkipInit(out positionInSequence);
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments;
            if (segmentPosition >= s.Length) return false;
            var ss = s[segmentPosition];
            var upperBound = ss.Memory.Length;
            if (segmentPosition == 0 && index < firstIndex) return false;
            if (segmentPosition == s.Length - 1)
            {
                upperBound = endIndex;
            }
            if (index >= upperBound) return false;
            segment = ss;
            positionInSequence = ss.RunningIndex + index - firstRunningIndex;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private void AssertPosition(SequencePositionSlim position, out ReadOnlySequenceSegment<T> segment, out nuint positionInSequence, [ConstantExpected] bool allowEnd = false)
        {
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments;
            if (allowEnd)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(segmentPosition, s.Length);
            }
            else
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(segmentPosition, s.Length);
            }
            var ss = s[segmentPosition];
            var upperBound = ss.Memory.Length;
            if (segmentPosition == 0)
            {
                ArgumentOutOfRangeException.ThrowIfLessThan(index, firstIndex);
            }

            var v = segmentPosition == s.Length - 1;
            if (v)
            {
                upperBound = endIndex;
            }
            if (v && allowEnd)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(index, upperBound);
            }
            else
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, upperBound);
            }
            segment = ss;
            positionInSequence = ss.RunningIndex + index - firstRunningIndex;
        }

        public SequencePositionSlim GetPosition(nuint offset)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, Length);
            return FindPosition(segments, offset + firstRunningIndex + firstIndex);
        }
        public SequencePositionSlim GetPosition(nuint offset, SequencePositionSlim origin)
        {
            var position = origin;
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(segmentPosition, s.Length);
            var ss = s.ElementAtUnchecked(segmentPosition);
            nuint lowerBound = 0;
            var upperBound = ss.Memory.Length;
            if (segmentPosition == s.Length - 1)
            {
                upperBound = endIndex;
            }
            if (segmentPosition == 0)
            {
                lowerBound = firstIndex;
            }
            index -= lowerBound;
            upperBound -= lowerBound;
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, upperBound);
            var n = index + offset;
            if (n < upperBound)
            {
                return new(segmentPosition, n + lowerBound);
            }
            n -= upperBound;
            if (++segmentPosition >= s.Length)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(n, (nuint)0);
                return End;
            }
            var newSegment = s.ElementAtUnchecked(segmentPosition);
            var length = newSegment.Memory.Length;
            if (n < length || segmentPosition == s.Length - 1 && n == length)
            {
                return new(segmentPosition, n);
            }
            // binary search fallback from 2nd next position
            n += newSegment.RunningIndex + lowerBound;
            segmentPosition++;
            return FindPosition(s.Slice(segmentPosition), n, segmentPosition);
        }

        internal static SequencePositionSlim FindPosition(ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>> segments, nuint index, nuint positionOffset = 0)
        {
            var pos = segments.BinarySearchRangeComparable(index, out _);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(pos, segments.Length);
            if (pos == segments.Length)
            {
                ArgumentOutOfRangeException.ThrowIfZero(segments.Length);
                var tail = segments.Tail;
                var newIndex = index - tail.RunningIndex;
                ArgumentOutOfRangeException.ThrowIfGreaterThan(newIndex, tail.Memory.Length);
                return new(segments.Length - 1 + positionOffset, newIndex);
            }
            var nsg = segments.ElementAtUnchecked(pos);
            return new(pos + positionOffset, index - nsg.RunningIndex);
        }

        public ReadOnlySequenceSlim<T> Slice(nuint start) => start == 0 ? this : SliceInternal(GetPosition(start));
        public ReadOnlySequenceSlim<T> Slice(SequencePositionSlim start)
        {
            AssertPosition(start, out _, out var positionInSequence);
            return positionInSequence == 0 ? this : SliceInternal(start);
        }
        public ReadOnlySequenceSlim<T> Slice(nuint start, nuint length)
            => start == 0 ? SliceInternalByEnd(GetPosition(length)) : Slice(GetPosition(start), length);
        public ReadOnlySequenceSlim<T> Slice(nuint start, SequencePositionSlim end)
        {
            AssertPosition(end, out _, out _, true);
            return start == 0 ? SliceInternalByEnd(end) : Slice(GetPosition(start), end);
        }

        public ReadOnlySequenceSlim<T> Slice(SequencePositionSlim start, nuint length)
        {
            AssertPosition(start, out _, out var positionInSequence);
            return positionInSequence == 0 ? SliceInternalByEnd(GetPosition(length)) : SliceInternal(start, GetPosition(length, start));
        }
        public ReadOnlySequenceSlim<T> Slice(SequencePositionSlim start, SequencePositionSlim end)
        {
            AssertPosition(start, out _, out var positionInSequence);
            AssertPosition(end, out _, out _, true);
            return positionInSequence == 0 ? SliceInternalByEnd(end) : SliceInternal(start, end);
        }

        private ReadOnlySequenceSlim<T> SliceInternal(SequencePositionSlim start)
        {
            var position = start;
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var newSegments = segments.Slice(segmentPosition);
            return new(newSegments, index, endIndex);
        }

        private ReadOnlySequenceSlim<T> SliceInternal(SequencePositionSlim start, SequencePositionSlim end)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(end, start);
            if (end == start) return Empty;
            var firstSegmentPosition = start.SegmentPosition;
            var lastSegmentPosition = end.SegmentPosition;
            var headIndex = start.Index;
            var lastIndex = end.Index;
            var newSegments = segments.Slice(firstSegmentPosition, lastSegmentPosition - firstSegmentPosition + 1);
            return new(newSegments, headIndex, lastIndex);
        }

        private ReadOnlySequenceSlim<T> SliceInternalByEnd(SequencePositionSlim end)
        {
            var position = end;
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var newSegments = segments.Slice(0, segmentPosition + 1);
            return new(newSegments, firstIndex, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryGet(SequencePositionSlim position, out ReadOnlyNativeMemory<T> memory, out SequencePositionSlim newPosition)
        {
            Unsafe.SkipInit(out memory);
            Unsafe.SkipInit(out newPosition);
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments;
            if (segmentPosition >= s.Length) return false;
            var ss = s[segmentPosition];
            var newMemory = ss.Memory;
            var upperBound = newMemory.Length;
            if (segmentPosition == 0 && index < firstIndex) return false;
            if (segmentPosition == s.Length - 1)
            {
                upperBound = endIndex;
                newMemory = newMemory.Slice(upperBound);
            }
            if (index >= upperBound) return false;
            memory = newMemory.Slice(index);
            newPosition = new(segmentPosition + 1, 0);
            return true;
        }

        public Enumerator GetEnumerator() => new(this);

        public T[] ToArray() => [.. this];

        public ref struct Enumerator
        {
            private ReadOnlySequenceSlim<T> sequence;
            private nuint segmentIndex;
            private ReadOnlyNativeMemory<T> currentMemory;
            private nuint index;

            public Enumerator(ReadOnlySequenceSlim<T> sequence) : this()
            {
                this.sequence = sequence;
                ResetInternal(sequence);
            }

            public readonly T Current
            {
                get
                {
                    var s = currentMemory.Span;
                    return index < s.Length ? s[index] : default!;
                }
            }

            public void Dispose()
            {
                sequence = default;
                (segmentIndex, currentMemory, index) = (default, default, default);
            }

            public bool MoveNext()
            {
                var i = index + 1;
                if (i >= currentMemory.Length)
                {
                    var span = sequence.segments;
                    if (++segmentIndex >= span.Length) return false;
                    currentMemory = span[segmentIndex].Memory;
                    i = 0;
                }
                index = i;
                return true;
            }
            public void Reset() => ResetInternal(sequence);

            private void ResetInternal(ReadOnlySequenceSlim<T> s)
            {
                if (!s.IsEmpty)
                {
                    var ss = s.segments;
                    var c = ss[0];
                    segmentIndex = 0;
                    currentMemory = c.Memory;
                    index = s.firstIndex - 1;
                }
            }
        }
    }

    public static class ReadOnlySequenceSlim
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ReadOnlySequenceSlim<T> Create<T>(ReadOnlyNativeSpan<ReadOnlyNativeMemory<T>> segments, out INativeMemoryOwner<ReadOnlySequenceSegment<T>>? owner)
        {
            if (segments.FitsInArray)
            {
                owner = default;
                return new(segments.GetHeadReadOnlySpan());
            }
            var array = new PooledArray<ReadOnlySequenceSegment<T>>(segments.Length);
            var span = array.Span.Slice(0, segments.Length);
            nuint runningIndex = 0;
            for (nuint i = 0; i < span.Length; i++)
            {
                var segment = segments.ElementAtUnchecked(i);
                span.ElementAtUnchecked(i) = new(segment, runningIndex);
                runningIndex += segment.Length;
            }
            owner = array;
            return new(array.Span);
        }

        public static ReadOnlySequenceSlim<T> Create<T>(ReadOnlySpan<T[]> arrays)
            => new(arrays);
        public static ReadOnlySequenceSlim<T> Create<T>(Span<T[]> arrays)
            => new(arrays);
    }
}
