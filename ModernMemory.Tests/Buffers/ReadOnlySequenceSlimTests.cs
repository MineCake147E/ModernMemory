using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Tests.Buffers
{
    [TestFixture]
    public class ReadOnlySequenceSlimTests
    {
        [TestCase(256)]
        public void SingleSegmentConstructsCorrectly(int length)
        {
            var array = new byte[length];
            var sequence = new ReadOnlySequenceSlim<byte>(array);
            Assert.That(sequence.Length, Is.EqualTo((nuint)array.Length));
        }

        [TestCase(256, 256)]
        [TestCase(256, 257, 255, 256)]
        public void MultipleSegmentsConstructsCorrectly(params int[] lengths)
        {
            var arrays = new byte[lengths.Length][];
            nuint sum = 0;
            for (var i = 0; i < arrays.Length; i++)
            {
                var v = lengths[i];
                arrays[i] = new byte[v];
                sum += (nuint)v;
            }
            Debug.Assert(arrays.Length == lengths.Length);
            var sequence = ReadOnlySequenceSlim.Create(arrays.AsSpan());
            Assert.That(sequence.Length, Is.EqualTo(sum));
        }

        [TestCase(1024)]
        [TestCase(256, 256)]
        [TestCase(256, 257, 255, 256)]
        public void EnumeratorWorksCorrectly(params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            Assert.That(sequence.ToArray(), Is.EquivalentTo(Enumerable.Range(0, sum)));
        }

        private static void PrepareIndexSequence(int[] lengths, out int sumResult, out ReadOnlySequenceSlim<int> sequence)
        {
            var arrays = new int[lengths.Length][];
            var sum = 0;
            for (var i = 0; i < arrays.Length; i++)
            {
                var v = lengths[i];
                arrays[i] = Enumerable.Range(sum, v).ToArray();
                sum += v;
                var y = arrays[i].ToList();
                Debug.WriteLine(y);
            }
            sumResult = sum;
            Debug.Assert(arrays.Length == lengths.Length);
            sequence = ReadOnlySequenceSlim.Create(arrays.AsSpan());
        }

        private static IEnumerable<TestCaseData> SliceByStartTestCaseSource()
        {
            int[][] lengths = [[1024], [512, 512], [256, 257, 254, 257]];
            int[] positions = [0, 256, 512, 768];
            return lengths.SelectMany(a => positions.Select(b => new TestCaseData(b, a)));
        }

        [TestCaseSource(nameof(SliceByStartTestCaseSource))]
        public void SliceByStartWorksCorrectly(int start, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var slice = sequence.Slice((nuint)start);
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        [TestCaseSource(nameof(SliceByStartTestCaseSource))]
        public void SliceByStartPositionWorksCorrectly(int start, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var pos = sequence.GetPosition((nuint)start);
            var slice = sequence.Slice(pos);
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        private static IEnumerable<TestCaseData> SliceByStartLengthTestCaseSource()
        {
            int[][] segmentLengths = [[1024], [512, 512], [256, 257, 254, 257]];
            (int start, int length)[] positions = [(0, 1023), (0, 1024), (256, 512), (256, 768), (256, 513), (512, 512), (767, 257), (768, 255), (768, 256)];
            return segmentLengths.SelectMany(a => positions.Select(b => new TestCaseData(b.start, b.length, a)));
        }

        [TestCaseSource(nameof(SliceByStartLengthTestCaseSource))]
        public void SliceByStartLengthWorksCorrectly(int start, int length, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var slice = sequence.Slice((nuint)start, (nuint)length);
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        [TestCaseSource(nameof(SliceByStartLengthTestCaseSource))]
        public void SliceByStartEndPositionWorksCorrectly(int start, int length, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var end = sequence.GetPosition((nuint)(start + length));
            var slice = sequence.Slice((nuint)start, end);
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        [TestCaseSource(nameof(SliceByStartLengthTestCaseSource))]
        public void SliceByStartPositionLengthWorksCorrectly(int start, int length, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var pos = sequence.GetPosition((nuint)start);
            var slice = sequence.Slice(pos, (nuint)length);
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        [TestCaseSource(nameof(SliceByStartLengthTestCaseSource))]
        public void SliceByStartPositionEndPositionWorksCorrectly(int start, int length, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var pos = sequence.GetPosition((nuint)start);
            var end = sequence.GetPosition((nuint)(start + length));
            var slice = sequence.Slice(pos, end);
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        private static IEnumerable<TestCaseData> SliceByStartAfterSliceTestCaseSource()
        {
            int[][] lengths = [[1024], [512, 512], [256, 257, 254, 257]];
            (SliceData first, int second)[] positions = [
                (new(255), 512), (new(256), 512), (new(256), 513), (new(512), 256), (new(767), 255), (new(768), 127), (new(768), 128),
                (new(255, 768), 512), (new(256, 767), 512), (new(256, 512), 256), (new(512, 511), 256), (new(767, 257), 255), (new(768, 255), 127), (new(768, 255), 128)];
            return lengths.SelectMany(a => positions.Select(b => new TestCaseData(b.first, b.second, a)));
        }

        [TestCaseSource(nameof(SliceByStartAfterSliceTestCaseSource))]
        public void SliceByStartWorksCorrectlyAfterSlice(SliceData first, int second, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var firstSlice = sequence.Slice(first);
            var slice = firstSlice.Slice((nuint)second);
            var start = (int)first.Start + second;
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        [TestCaseSource(nameof(SliceByStartAfterSliceTestCaseSource))]
        public void SliceByStartPositionWorksCorrectlyAfterSlice(SliceData first, int second, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var firstSlice = sequence.Slice(first);
            var pos = firstSlice.GetPosition((nuint)second);
            var slice = firstSlice.Slice(pos);
            var start = (int)first.Start + second;
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        private static IEnumerable<TestCaseData> SliceByStartLengthAfterSliceTestCaseSource()
        {
            int[][] segmentLengths = [[1024], [512, 512], [256, 257, 254, 257]];
            (SliceData first, int start, int length)[] positions = [
                (new(255), 512, 256), (new(256), 512, 255), (new(256), 513, 255), (new(512), 256, 255), (new(767), 255, 2),
                (new(255, 768), 512, 256), (new(256, 767), 512, 255), (new(256, 512), 256, 256), (new(512, 511), 256, 255),
                (new(767, 257), 2, 255), (new(768, 255), 127, 1), (new(768, 255), 128, 2)];
            return segmentLengths.SelectMany(a => positions.Select(b => new TestCaseData(b.first, b.start, b.length, a)));
        }

        [TestCaseSource(nameof(SliceByStartLengthAfterSliceTestCaseSource))]
        public void SliceByStartLengthWorksCorrectlyAfterSlice(SliceData first, int second, int length, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var firstSlice = sequence.Slice(first);
            var slice = firstSlice.Slice((nuint)second, (nuint)length);
            var start = (int)first.Start + second;
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        [TestCaseSource(nameof(SliceByStartLengthAfterSliceTestCaseSource))]
        public void SliceByStartEndPositionWorksCorrectlyAfterSlice(SliceData first, int second, int length, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var firstSlice = sequence.Slice(first);
            var end = firstSlice.GetPosition((nuint)second + (nuint)length);
            var slice = firstSlice.Slice((nuint)second, end);
            var start = (int)first.Start + second;
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        [TestCaseSource(nameof(SliceByStartLengthAfterSliceTestCaseSource))]
        public void SliceByStartPositionLengthWorksCorrectlyAfterSlice(SliceData first, int second, int length, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var firstSlice = sequence.Slice(first);
            var pos = firstSlice.GetPosition((nuint)second);
            var slice = firstSlice.Slice(pos, (nuint)length);
            var start = (int)first.Start + second;
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }

        [TestCaseSource(nameof(SliceByStartLengthAfterSliceTestCaseSource))]
        public void SliceByStartPositionEndPositionWorksCorrectlyAfterSlice(SliceData first, int second, int length, params int[] lengths)
        {
            PrepareIndexSequence(lengths, out var sum, out var sequence);
            var firstSlice = sequence.Slice(first);
            var pos = firstSlice.GetPosition((nuint)second);
            var end = firstSlice.GetPosition((nuint)second + (nuint)length);
            var slice = firstSlice.Slice(pos, end);
            var start = (int)first.Start + second;
            Assert.That(slice.ToArray(), Is.EquivalentTo(Enumerable.Range(start, sum - start)));
        }
    }
}
