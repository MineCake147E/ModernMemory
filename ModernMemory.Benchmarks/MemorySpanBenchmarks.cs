using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using ModernMemory.Allocation;
using ModernMemory.Buffers;

namespace ModernMemory.Benchmarks
{
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    public class MemorySpanBenchmarks
    {
        ReadOnlyMemory<char> memory;
        ReadOnlyNativeMemory<char> nativeMemory;
        string? str;
        char[]? array;
        NativeMemoryRegionOwner<char>? owner;

        //[ParamsAllValues]
        [Params(MemoryType.NativeMemoryManager, MemoryType.MemoryManager)]
        public MemoryType Type { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            str = $"{Environment.StackTrace}";
            switch (Type)
            {
                case MemoryType.String:
                    memory = str.AsMemory();
                    break;
                case MemoryType.Array:
                    array = str.ToCharArray();
                    memory = array.AsMemory();
                    break;
                default:
                    owner = new((uint)str.Length);
                    memory = owner.Memory;
                    break;
            }
            nativeMemory = owner is not null && Type == MemoryType.NativeMemoryManager ? owner.NativeMemory : memory;
        }

        [Benchmark]
        public ReadOnlySpan<char> MemoryGetSpan() => memory.Span;

        [Benchmark]
        public ReadOnlyNativeSpan<char> NativeMemoryGetSpan() => nativeMemory.Span;

        [GlobalCleanup]
        public void Cleanup()
        {
            memory = default;
            nativeMemory = default;
            array = default;
            owner?.Dispose();
        }
    }
}
