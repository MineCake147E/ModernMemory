using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace ModernMemory.Benchmarks
{
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    public class MoveMemoryBenchmarks
    {
        private byte[] bufferDst, bufferSrc;

        [Params(1073741823)]
        public int Size { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            bufferDst = GC.AllocateUninitializedArray<byte>(Size, true);
            bufferSrc = GC.AllocateUninitializedArray<byte>(Size, true);
        }

        [Benchmark]
        public void SpanCopyTo()
        {
            var ds = bufferDst.AsSpan();
            var ss = bufferSrc.AsSpan();
            ss.TryCopyTo(ds);
        }

        [Benchmark]
        public void MoveMemory()
        {
            var ds = bufferDst.AsSpan();
            var ss = bufferSrc.AsSpan();
            ref var x9 = ref MemoryMarshal.GetReference(ds);
            ref var x10 = ref MemoryMarshal.GetReference(ss);
            NativeMemoryUtils.MoveMemory(ref x9, ref x10, (nuint)ss.Length);
        }
    }
}
