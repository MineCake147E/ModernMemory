using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using ModernMemory.Sorting;
using ModernMemory.Utils;

namespace ModernMemory.Benchmarks.Sorting
{
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    public class AdaptiveOptimizedGrailSortBenchmarks
    {
        private ulong[] bufferDst, bufferSrc;

        [Params(1048576)]
        public int Size { get; set; }

        [Params(2048)]
        public int UniqueValues { get; set; }

        private static void GenerateValues(Span<ulong> values, int uniqueValues = -1)
        {
            if (uniqueValues >= 0)
            {
                if ((uint)uniqueValues < 2)
                {
                    values.Clear();
                    return;
                }
                var j = 0;
                var (streak, maxExtendedValues) = int.DivRem(values.Length, uniqueValues);
                var rem = values;
                for (; !rem.IsEmpty && j < maxExtendedValues; j++)
                {
                    var m = rem.SliceWhileIfLongerThan(streak + 1);
                    m.Fill((ulong)j);
                    rem = rem.Slice(m.Length);
                }
                for (; !rem.IsEmpty && j < uniqueValues; j++)
                {
                    var m = rem.SliceWhileIfLongerThan(streak);
                    m.Fill((ulong)j);
                    rem = rem.Slice(m.Length);
                }
            }
            else
            {
                for (var i = 0; i < values.Length; i++)
                {
                    var ui = (uint)i;
                    values[i] = ui >> 1;
                }
            }
        }

        [GlobalSetup]
        public void Setup()
        {
            bufferDst = GC.AllocateUninitializedArray<ulong>(Size, true);
            bufferSrc = GC.AllocateUninitializedArray<ulong>(Size, true);
            var values = bufferSrc.AsSpan();
            GenerateValues(values, UniqueValues);
            RandomNumberGenerator.Shuffle(values);
        }

        //[Benchmark(Baseline = true)]
        public void Copy()
        {
            var ds = bufferDst.AsSpan();
            var ss = bufferSrc.AsSpan();
            ss.TryCopyTo(ds);
        }

        [Benchmark]
        public void SortStandard()
        {
            var ds = bufferDst.AsSpan();
            var ss = bufferSrc.AsSpan();
            ss.TryCopyTo(ds);
            ds.Sort();
        }

        [Benchmark]
        public void SortAdaptiveOptimizedGrailSort()
        {
            var ds = bufferDst.AsSpan();
            var ss = bufferSrc.AsSpan();
            ss.TryCopyTo(ds);
            AdaptiveOptimizedGrailSort.Sort(ds.AsNativeSpan(), StaticComparer.Create<ulong, ComparisonOperatorsStaticComparer<ulong>>());
        }
    }
}
