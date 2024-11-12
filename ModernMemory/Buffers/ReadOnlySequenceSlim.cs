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
using ModernMemory.DataFlow;

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
        public ReadOnlySequenceSlim(ref readonly ReadOnlySequenceSegment<T> segment)
        {
            this = new(new ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>>(in segment));
        }

        [SkipLocalsInit]
        public ReadOnlySequenceSlim(ReadOnlyNativeMemory<T> memory, ref ReadOnlySequenceSegment<T> segment)
        {
            segment = new ReadOnlySequenceSegment<T>(memory, 0);
            this = new(ref segment);
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
            this = new((ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>>)array);
        }

        [SkipLocalsInit]
        public ReadOnlySequenceSlim(NativeSpan<ReadOnlySequenceSegment<T>> segments)
        {
            var span = segments;
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
            this = new((ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>>)span);
        }

        [EditorBrowsable(EditorBrowsableState.Advanced)]
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

        public nuint SegmentCount => segments.Length;

        public bool IsEmpty => Length == 0;
        public bool IsSingleSegment => segments.Length == 1;
        public SlimSequencePosition Start => IsEmpty ? default : new(0, firstIndex);
        public SlimSequencePosition End => IsEmpty ? default : new(segments.Length - 1, endIndex);
        public ReadOnlyNativeMemory<T> First => segments.IsEmpty ? default : segments[0].Memory;
        public ReadOnlyNativeSpan<T> FirstSpan => First.Span;

        public nuint GetOffset(SlimSequencePosition position)
        {
            AssertPosition(position, out _, out var offset);
            return offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private bool VerifyPosition(SlimSequencePosition position, out ReadOnlySequenceSegment<T> segment, out nuint positionInSequence)
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
        private void AssertPosition(SlimSequencePosition position, out ReadOnlySequenceSegment<T> segment, out nuint positionInSequence, [ConstantExpected] bool allowEnd = false)
        {
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments;
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(segmentPosition, s.Length);
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
            return FindPosition(segments, offset + firstRunningIndex + firstIndex);
        }
        public SlimSequencePosition GetPosition(nuint offset, SlimSequencePosition origin)
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

        public ReadOnlySequenceSlim<T> Slice(nuint start) => start == 0 ? this : SliceInternal(GetPosition(start));
        public ReadOnlySequenceSlim<T> Slice(SlimSequencePosition start)
        {
            AssertPosition(start, out _, out var positionInSequence);
            return positionInSequence == 0 ? this : SliceInternal(start);
        }
        public ReadOnlySequenceSlim<T> Slice(nuint start, nuint length)
            => start == 0 ? SliceInternalByEnd(GetPosition(length)) : Slice(GetPosition(start), length);
        public ReadOnlySequenceSlim<T> Slice(nuint start, SlimSequencePosition end)
        {
            AssertPosition(end, out _, out _, true);
            return start == 0 ? SliceInternalByEnd(end) : Slice(GetPosition(start), end);
        }

        public ReadOnlySequenceSlim<T> Slice(SlimSequencePosition start, nuint length)
        {
            AssertPosition(start, out _, out var positionInSequence);
            return positionInSequence == 0 ? SliceInternalByEnd(GetPosition(length)) : SliceInternal(start, GetPosition(length, start));
        }
        public ReadOnlySequenceSlim<T> Slice(SlimSequencePosition start, SlimSequencePosition end)
        {
            AssertPosition(start, out _, out var positionInSequence);
            AssertPosition(end, out _, out _, true);
            return positionInSequence == 0 ? SliceInternalByEnd(end) : SliceInternal(start, end);
        }

        private ReadOnlySequenceSlim<T> SliceInternal(SlimSequencePosition start)
        {
            var position = start;
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var newSegments = segments.Slice(segmentPosition);
            return new(newSegments, index, endIndex);
        }

        private ReadOnlySequenceSlim<T> SliceInternal(SlimSequencePosition start, SlimSequencePosition end)
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

        private ReadOnlySequenceSlim<T> SliceInternalByEnd(SlimSequencePosition end)
        {
            var position = end;
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var newSegments = segments.Slice(0, segmentPosition + 1);
            return new(newSegments, firstIndex, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryGet(SlimSequencePosition position, out ReadOnlyNativeMemory<T> memory, out SlimSequencePosition newPosition)
        {
            Unsafe.SkipInit(out memory);
            Unsafe.SkipInit(out newPosition);
            var segmentPosition = position.SegmentPosition;
            var index = position.Index;
            var s = segments;
            if (segmentPosition >= s.Length) return false;
            var ss = s.ElementAtUnchecked(segmentPosition);
            var newMemory = ss.Memory;
            if (segmentPosition == 0 && index < firstIndex) return false;
            if (segmentPosition == s.Length - 1)
            {
                newMemory = newMemory.Slice(0, endIndex);
            }
            if (index >= newMemory.Length) return false;
            memory = newMemory.Slice(index);
            var pos = new SlimSequencePosition(++segmentPosition, 0);
            if (segmentPosition == s.Length) pos = End;
            newPosition = pos;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryGetSegmentAt(nuint segmentPosition, out ReadOnlyNativeMemory<T> memory)
        {
            Unsafe.SkipInit(out memory);
            var index = (nuint)0;
            var s = segments;
            if (segmentPosition >= s.Length) return false;
            var ss = s.ElementAtUnchecked(segmentPosition);
            var newMemory = ss.Memory;
            if (segmentPosition == s.Length - 1)
            {
                newMemory = newMemory.Slice(0, endIndex);
            }
            if (index >= newMemory.Length) return false;
            memory = newMemory.Slice(index);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryGetSegmentAt(nuint segmentPosition, out ReadOnlyNativeMemory<T> memory, out SlimSequencePosition newPosition)
        {
            Unsafe.SkipInit(out memory);
            Unsafe.SkipInit(out newPosition);
            var index = (nuint)0;
            var s = segments;
            if (segmentPosition >= s.Length) return false;
            var ss = s.ElementAtUnchecked(segmentPosition);
            var newMemory = ss.Memory;
            if (segmentPosition == s.Length - 1)
            {
                newMemory = newMemory.Slice(0, endIndex);
            }
            if (index >= newMemory.Length) return false;
            memory = newMemory.Slice(index);
            var pos = new SlimSequencePosition(++segmentPosition, 0);
            if (segmentPosition >= s.Length) pos = End;
            newPosition = pos;
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool IsInRange(SlimSequencePosition position) => VerifyPosition(position, out _, out _);

        public Enumerator GetEnumerator() => new(this);

        public T[] ToArray()
        {
            var array = new T[Length];
            var dataWriter = DataWriter.CreateFrom(array.AsNativeSpan());
            dataWriter.WriteAtMost(this);
            return array;
        }

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
                    var memory = span[segmentIndex].Memory;
                    if (segmentIndex == span.Length - 1) memory = memory.Slice(0, sequence.endIndex);
                    currentMemory = memory;
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
                    var c = ss.Head;
                    segmentIndex = 0;
                    var memory = c.Memory;
                    if (ss.Length == 1) memory = memory.Slice(0, s.endIndex);
                    currentMemory = memory;
                    index = s.firstIndex - 1;
                }
            }
        }

        public readonly SegmentList GetSegmentsEnumerable() => new(this);

        public readonly ref struct SegmentList
        {
            private readonly ReadOnlySequenceSlim<T> sequence;

            internal SegmentList(ReadOnlySequenceSlim<T> sequence)
            {
                this.sequence = sequence;
            }

            public readonly ReadOnlyNativeMemory<T> this[nuint segmentIndex]
            {
                get
                {
                    var span = sequence.segments;
                    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(segmentIndex, span.Length);
                    var segment = span.ElementAtUnchecked(segmentIndex);
                    var memory = segment.Memory;
                    if (segmentIndex == span.Length - 1) memory = memory.Slice(0, sequence.endIndex);
                    if (segmentIndex == 0) memory = memory.Slice(sequence.firstIndex);
                    return memory;
                }
            }

            public SegmentEnumerator GetEnumerator() => new(sequence);

            public ref struct SegmentEnumerator
            {
                private ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>> segments;
                private nuint index;
                private readonly nuint firstIndex;
                private readonly nuint endIndex;

                internal SegmentEnumerator(ReadOnlySequenceSlim<T> sequence)
                {
                    this = new(sequence.segments, sequence.firstIndex, sequence.endIndex);
                }

                internal SegmentEnumerator(ReadOnlyNativeSpan<ReadOnlySequenceSegment<T>> segments, nuint firstIndex, nuint endIndex)
                {
                    this.segments = segments;
                    this.firstIndex = firstIndex;
                    this.endIndex = endIndex;
                    index = ~(nuint)0;
                }

                public readonly ReadOnlyNativeMemory<T> Current
                {
                    get
                    {
                        var s = segments;
                        if (index >= s.Length) return default;
                        var segment = s.ElementAtUnchecked(0).Memory;
                        if (index == s.Length - 1)
                        {
                            segment = segment.Slice(0, endIndex);
                        }
                        if (index == 0)
                        {
                            segment = segment.Slice(firstIndex);
                        }
                        return segment;
                    }
                }

                public void Dispose()
                {
                    segments = default;
                    index = default;
                }

                public bool MoveNext()
                {
                    var i = ++index;
                    return i < segments.Length;
                }
                public void Reset() => index = ~(nuint)0;
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
            var array = new MemoryArray<ReadOnlySequenceSegment<T>>(segments.Length);
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
