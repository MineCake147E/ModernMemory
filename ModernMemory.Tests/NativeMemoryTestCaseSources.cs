using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Tests
{
    internal static class NativeMemoryTestCaseSources<T, TSequenceProvider>
        where TSequenceProvider : ISequenceProvider<T>
    {
        private static IEnumerable<(MemoryType type, object? medium, nuint length)> MemoryValues()
        {
            var array = new T[128];
            TSequenceProvider.GenerateSequence(array);
            if (typeof(T) == typeof(char))
            {
                var str = string.Join("", Enumerable.Range(0, 128).Select(a => $"{a & 0xf:X}"));
                yield return (MemoryType.String, str, (uint)str.Length);
            }
            else
            {
                yield return (MemoryType.Array, array, (nuint)array.Length);
            }
            var manager = new ArrayMemoryManager<T>(array);
            yield return (MemoryType.MemoryManager, manager, manager.Length);
            yield return (MemoryType.NativeMemoryManager, manager, manager.Length);
        }

        private static IEnumerable<SliceData> GenerateSlices(nuint length)
        {
            var baseSlice = new SliceData(0, length);
            yield return baseSlice;
            var halfLength = length / 2;
            yield return baseSlice.Slice(0, halfLength);
            yield return baseSlice.Slice(halfLength);
            var thirdLength = length / 3;
            yield return baseSlice.Slice(0, thirdLength);
            yield return baseSlice.Slice(thirdLength, thirdLength);
            yield return baseSlice.Slice(length - thirdLength);
        }

        internal static IEnumerable<TestCaseData> SpanTestCaseSource()
            => MemoryValues().SelectMany(a => GenerateSlices(a.length).Distinct().Select(b => new TestCaseData(a.type, a.medium, a.length, b)));
    }
}
