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

        [Params((1 << 17) - 1, (1 << 20) - 1)]
        public int Capacity { get; set; }

        private const int OperationsPerInvoke = 1 << 16;

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
                    for (int i = 0; i < 1024; i++)
                    {
                        queue?.Clear();
                    }
                }
            });
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void TryAddUntilSuccessSingle()
        {
            var q = queue;
            ArgumentNullException.ThrowIfNull(q);
            var w = q.GetWriter();
            for (int i = 0; i < OperationsPerInvoke; i++)
            {
                while (!w.TryAdd(i))
                {
                    // try again
                }
            }
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
