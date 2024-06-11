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
        MemoryManager<char>? memoryManager;
        NativeMemoryManager<char>? nativeMemoryManager;

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
                case MemoryType.MemoryManager:
                    memoryManager = new NativeMemoryRegionMemoryManager<char>(new((uint)str.Length));
                    memory = memoryManager.Memory;
                    break;
                default:
                    nativeMemoryManager = new NativeMemoryRegionMemoryManager<char>(new((uint)str.Length));
                    memory = nativeMemoryManager.Memory;
                    break;
            }
            nativeMemory = nativeMemoryManager is not null ? nativeMemoryManager.NativeMemory : memory;
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
            (memoryManager as IDisposable)?.Dispose();
            (nativeMemoryManager as IDisposable)?.Dispose();
        }
    }
}
