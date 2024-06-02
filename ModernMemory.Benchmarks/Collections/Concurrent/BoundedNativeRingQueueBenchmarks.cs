using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using ModernMemory.Collections.Concurrent;
using ModernMemory.Threading;

namespace ModernMemory.Benchmarks.Collections.Concurrent
{
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    public class BoundedNativeRingQueueBenchmarks
    {
        private BoundedNativeRingQueue<int>? queue;
        private Task? clearTask;
        private CancellationTokenSource? tokenSource;

        [Params(255, 65535)]
        public int Capacity { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            queue = new((nuint)Capacity);
            tokenSource = new();
            clearTask = Task.Run(async () =>
            {
                await Task.Yield();
                while (!tokenSource.IsCancellationRequested)
                {
                    queue?.Clear();
                }
            });
        }

        [Benchmark]
        public void TryAddSingleAndClear()
        {
            queue?.TryAdd(7);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            tokenSource?.Cancel();
            clearTask?.Wait();
            queue?.Dispose();
            tokenSource?.Dispose();
        }
    }
}
