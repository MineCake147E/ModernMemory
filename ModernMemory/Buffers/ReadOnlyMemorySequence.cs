using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections;

namespace ModernMemory.Buffers
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly partial struct ReadOnlyMemorySequence<T> : IReadOnlySequence<T, ReadOnlyMemorySequence<T>, SlimSequencePosition, ReadOnlyMemorySequence<T>.Enumerator>
    {
        private readonly ReadOnlyNativeMemory<ReadOnlySequenceSegment<T>> segments;
        private readonly nuint firstIndex;
        private readonly nuint endIndex;
        private readonly nuint firstRunningIndex;
        private readonly nuint lastRunningIndex;

        [SkipLocalsInit]
        public ReadOnlyMemorySequence(ReadOnlyNativeMemory<T> memory)
        {
            Unsafe.SkipInit(out this);
            var segment = new ReadOnlySequenceSegment<T>(memory, 0);
            this = new(segment);
        }

        [SkipLocalsInit]
        public ReadOnlyMemorySequence(ReadOnlySpan<ReadOnlyNativeMemory<T>> segments)
        {
            Unsafe.SkipInit(out this);
            var array = new ReadOnlySequenceSegment<T>[segments.Length];
            var span = array.AsSpan(0, segments.Length);
            ConstructSegments(0, segments, span);
            this = new(array);
        }

        [SkipLocalsInit]
        public ReadOnlyMemorySequence(ReadOnlyNativeSpan<ReadOnlyNativeMemory<T>> segments, ref MemoryResizer<ReadOnlySequenceSegment<T>> memory)
        {
            Unsafe.SkipInit(out this);
            memory.Resize(segments.Length);
            var array = memory.NativeMemory;
            var span = array.Span;
            ConstructSegments(0, segments, span);
            this = new(array);
        }

        [SkipLocalsInit]
        public ReadOnlyMemorySequence(ReadOnlySpan<T[]> segments)
        {
            Unsafe.SkipInit(out this);
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

        [SkipLocalsInit]
        public ReadOnlyMemorySequence(NativeMemory<ReadOnlySequenceSegment<T>> segments)
        {
            var span = segments.Span;
            if (span.IsEmpty)
            {
                this = default;
                return;
            }
            nuint runningIndex = 0;
            for (nuint i = 0; i < span.Length; i++)
            {
                ref var m = ref span.ElementAtUnchecked(i);
                var memory = m.Memory;
                m = new(memory, runningIndex);
                runningIndex += memory.Length;
            }
            this = new((ReadOnlyNativeMemory<ReadOnlySequenceSegment<T>>)segments);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyMemorySequence(params ReadOnlySequenceSegment<T>[] segments)
        {
            this = new(segments.AsMemory());
        }

        [SkipLocalsInit]
        internal ReadOnlyMemorySequence(ReadOnlyNativeMemory<ReadOnlySequenceSegment<T>> segments)
        {
            var span = segments.Span;
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

        [SkipLocalsInit]
        internal ReadOnlyMemorySequence(ReadOnlyNativeMemory<ReadOnlySequenceSegment<T>> segments, nuint firstIndex, nuint endIndex)
        {
            var span = segments.Span;
            if (span.IsEmpty)
            {
                this = default;
                return;
            }
            var head = span.Head;
            var tail = span.Tail;
            var headRunningIndex = head.RunningIndex;
            var tailRunningIndex = tail.RunningIndex;
            var last = tailRunningIndex + endIndex;
            var first = headRunningIndex + firstIndex;
            if (last == first)  // Empty
            {
                this = default;
                return;
            }
            ArgumentOutOfRangeException.ThrowIfLessThan(tailRunningIndex, headRunningIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(firstIndex, head.Memory.Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(endIndex, tail.Memory.Length);
            ArgumentOutOfRangeException.ThrowIfLessThan(last, first);
            this.segments = segments;
            this.firstIndex = firstIndex;
            this.endIndex = endIndex;
            firstRunningIndex = headRunningIndex;
            lastRunningIndex = tailRunningIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static nuint ConstructSegments(nuint initialRunningIndex, ReadOnlyNativeSpan<ReadOnlyNativeMemory<T>> segments, NativeSpan<ReadOnlySequenceSegment<T>> calculatedSegments)
        {
            nuint runningIndex = initialRunningIndex;
            var s = segments;
            var cs = calculatedSegments.SliceWhile(s.Length);
            for (nuint i = 0; i < s.Length; i++)
            {
                var segment = s.ElementAtUnchecked(i);
                cs.ElementAtUnchecked(i) = new(segment, runningIndex);
                runningIndex = checked(runningIndex + segment.Length);
            }
            return runningIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool ConstructSegments(ref nuint runningIndex, ReadOnlyNativeSpan<ReadOnlyNativeMemory<T>> segments, NativeSpan<ReadOnlySequenceSegment<T>> calculatedSegments)
        {
            nuint currentRunningIndex = runningIndex;
            var s = segments;
            var cs = calculatedSegments.SliceWhile(s.Length);
            bool success = true;
            for (nuint i = 0; success && i < s.Length; i++)
            {
                var segment = s.ElementAtUnchecked(i);
                cs.ElementAtUnchecked(i) = new(segment, currentRunningIndex);
                var newRunningIndex = currentRunningIndex + segment.Length;
                success &= newRunningIndex >= currentRunningIndex;
                currentRunningIndex = newRunningIndex;
            }
            if (success) runningIndex = currentRunningIndex;
            return success;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static nuint ReconstructSegments(NativeSpan<ReadOnlySequenceSegment<T>> calculatedSegments)
        {
            nuint currentRunningIndex = 0;
            if (!calculatedSegments.IsEmpty)
            {
                var cs = calculatedSegments;
                var headMemory = cs.Head.Memory;
                currentRunningIndex = headMemory.Length;
                cs.Head = new(headMemory, 0);
                cs = cs.Slice(1);
                for (nuint i = 0; i < cs.Length; i++)
                {
                    var segment = cs.ElementAtUnchecked(i).Memory;
                    cs.ElementAtUnchecked(i) = new(segment, currentRunningIndex);
                    currentRunningIndex = checked(currentRunningIndex + segment.Length);
                }
            }
            return currentRunningIndex;
        }

#pragma warning disable S1168 // Empty arrays and collections should be returned instead of null
        public static ReadOnlyMemorySequence<T> Empty => default;
#pragma warning restore S1168 // Empty arrays and collections should be returned instead of null

        public nuint Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => segments.IsEmpty ? 0 : lastRunningIndex + endIndex - (firstRunningIndex + firstIndex);
        }

        public bool IsEmpty => Length == 0;
        public bool IsSingleSegment => segments.Length == 1;
        public SlimSequencePosition Start => IsEmpty ? default : new(0, firstIndex);
        public SlimSequencePosition End => IsEmpty ? default : new(segments.Length - 1, endIndex);

        public ReadOnlyNativeMemory<T> First => segments.Span.IsEmpty ? ReadOnlyNativeMemory<T>.Empty : segments.Span[0].Memory;

        public ReadOnlyNativeSpan<T> FirstSpan => First.Span;

        public nuint GetOffset(SlimSequencePosition position)
        {
            AssertPosition(position, out _, out var offset, true);
            return offset;
        }

        public nuint GetSize(SlimSequencePosition start, SlimSequencePosition end)
        {
            if (start == end) return 0;
            AssertPosition(start, out _, out var startPosition);
            AssertPosition(end, out _, out var endPosition, true);
            return endPosition - startPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private bool VerifyPosition(SlimSequencePosition position, out ReadOnlySequenceSegment<T> segment, out nuint positionInSequence)
        {
            Unsafe.SkipInit(out segment);
            Unsafe.SkipInit(out positionInSequence);
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments.Span;
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
        private void AssertPosition(SlimSequencePosition position, out ReadOnlySequenceSegment<T> segment, out nuint positionInSequence, [ConstantExpected] bool allowEnd = false)
        {
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments.Span;
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

        public SlimSequencePosition GetPosition(nuint offset)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, Length);
            return FindPosition(segments.Span, offset + firstRunningIndex + firstIndex);
        }
        public SlimSequencePosition GetPosition(nuint offset, SlimSequencePosition origin)
        {
            var position = origin;
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments.Span;
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

        internal static SlimSequencePosition FindPosition(ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>> segments, nuint index, nuint positionOffset = 0)
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
        public ReadOnlyMemorySequence<T> Slice(nuint start) => start == 0 ? this : SliceInternal(GetPosition(start));
        public ReadOnlyMemorySequence<T> Slice(SlimSequencePosition start)
        {
            AssertPosition(start, out _, out var positionInSequence);
            return positionInSequence == 0 ? this : SliceInternal(start);
        }
        public ReadOnlyMemorySequence<T> Slice(nuint start, nuint length)
            => start == 0 ? SliceInternalByEnd(GetPosition(length)) : Slice(GetPosition(start), length);
        public ReadOnlyMemorySequence<T> Slice(nuint start, SlimSequencePosition end)
        {
            AssertPosition(end, out _, out _, true);
            return start == 0 ? SliceInternalByEnd(end) : Slice(GetPosition(start), end);
        }

        public ReadOnlyMemorySequence<T> Slice(SlimSequencePosition start, nuint length)
        {
            AssertPosition(start, out _, out var positionInSequence);
            return positionInSequence == 0 ? SliceInternalByEnd(GetPosition(length)) : SliceInternal(start, GetPosition(length, start));
        }
        public ReadOnlyMemorySequence<T> Slice(SlimSequencePosition start, SlimSequencePosition end)
        {
            AssertPosition(start, out _, out var positionInSequence);
            AssertPosition(end, out _, out _, true);
            return positionInSequence == 0 ? SliceInternalByEnd(end) : SliceInternal(start, end);
        }

        private ReadOnlyMemorySequence<T> SliceInternal(SlimSequencePosition start)
        {
            var position = start;
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var newSegments = segments.Slice(segmentPosition);
            return new(newSegments, index, endIndex);
        }

        private ReadOnlyMemorySequence<T> SliceInternal(SlimSequencePosition start, SlimSequencePosition end)
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

        private ReadOnlyMemorySequence<T> SliceInternalByEnd(SlimSequencePosition end)
        {
            var position = end;
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var newSegments = segments.Slice(0, segmentPosition + 1);
            return new(newSegments, firstIndex, index);
        }

        public bool TryGet(SlimSequencePosition position, out ReadOnlyNativeMemory<T> memory, out SlimSequencePosition newPosition)
        {
            Unsafe.SkipInit(out memory);
            Unsafe.SkipInit(out newPosition);
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments.Span;
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
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public Enumerator GetEnumerator() => new(this);

        public static implicit operator ReadOnlySequenceSlim<T>(ReadOnlyMemorySequence<T> self) => new(self.segments.Span, self.firstIndex, self.endIndex);

        public struct Enumerator : IEnumerator<T>
        {
            private ReadOnlyMemorySequence<T> sequence;
            private nuint segmentIndex;
            private ReadOnlyNativeMemory<T> currentMemory;
            private nuint index;

            public Enumerator(ReadOnlyMemorySequence<T> sequence) : this()
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

            readonly object IEnumerator.Current => Current!;

            public void Dispose() => (sequence, segmentIndex, currentMemory, index) = (default, default, default, default);
            public bool MoveNext()
            {
                var i = index + 1;
                if (i >= currentMemory.Length)
                {
                    var span = sequence.segments.Span;
                    if (++segmentIndex >= span.Length) return false;
                    var memory = span.ElementAtUnchecked(segmentIndex).Memory;
                    if (segmentIndex == span.Length - 1) memory = memory.Slice(0, sequence.endIndex);
                    currentMemory = memory;
                    i = 0;
                }
                index = i;
                return true;
            }
            public void Reset() => ResetInternal(sequence);

            private void ResetInternal(ReadOnlyMemorySequence<T> s)
            {
                if (!s.IsEmpty)
                {
                    var ss = s.segments.Span;
                    var c = ss[0];
                    segmentIndex = 0;
                    var memory = c.Memory;
                    if (ss.Length == 1) memory = memory.Slice(0, s.endIndex);
                    currentMemory = memory;
                    index = s.firstIndex - 1;
                }
            }
        }
    }

    public static class ReadOnlyMemorySequence
    {
        public static ReadOnlyMemorySequence<T> Create<T>(ReadOnlySpan<T[]> arrays)
            => new(arrays);
        public static ReadOnlyMemorySequence<T> Create<T>(Span<T[]> arrays)
            => new(arrays);
    }
}
