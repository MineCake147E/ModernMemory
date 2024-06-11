using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Allocation;
using ModernMemory.Buffers;
using ModernMemory.Tests.Utils;

namespace ModernMemory.Tests
{
    [TestFixture(typeof(char), typeof(BinaryIntegerSequenceProvider<char>))]
    [TestFixture(typeof(uint), typeof(BinaryIntegerSequenceProvider<uint>))]
    public class ReadOnlyNativeMemoryTests<T, TSequenceProvider>
        where TSequenceProvider : ISequenceProvider<T>
    {
        private static IEnumerable<TestCaseData> SpanTestCaseSource() => NativeMemoryTestCaseSources<T, TSequenceProvider>.SpanTestCaseSource();

        [TestCaseSource(nameof(SpanTestCaseSource))]
        public void SpanGetsCorrectly(MemoryType type, object? medium, nuint length, SliceData slice)
        {
            var mem = new ReadOnlyNativeMemory<T>(type, medium, 0, length);
            var exp = mem.Span.Slice(slice);
            var act = mem.Slice(slice).Span;
            Assert.That(act.ToArray(), Is.EqualTo(exp.ToArray()));
        }

        public void SliceSlicesCorrectly(int size, SliceData slice)
        {
            var array = new T[size];
            TSequenceProvider.GenerateSequence(array);
            var mem = ((ReadOnlyNativeMemory<T>)array.AsNativeMemory()).Slice(slice);
            Assert.That(mem.Span.ToArray(), Is.EqualTo(array.AsNativeSpan().Slice(slice).ToArray()));
        }
    }
}
