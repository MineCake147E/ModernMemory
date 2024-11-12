using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using ModernMemory.Collections.Concurrent;
using ModernMemory.Collections.Storage;
using ModernMemory.Threading;

namespace ModernMemory.Benchmarks.Collections.Concurrent
{
    [SimpleJob(runtimeMoniker: RuntimeMoniker.HostProcess)]
    [DisassemblyDiagnoser(maxDepth: int.MaxValue)]
    public class BoundedNativeRingQueueBenchmarks
    {
        private BoundedNativeRingQueue<int, ArrayStorage<int>>? queue;
        private Task? clearTask;
        private CancellationTokenSource? tokenSource;

        [Params((1 << 17) - 1, (1 << 20) - 1)]
        public int Capacity { get; set; }

        private const int OperationsPerInvoke = 1 << 12;

        [GlobalSetup(Target = nameof(BoundedNativeRingQueue))]
        public void SetupBoundedNativeRingQueue()
        {
            queue = new(new(Capacity));
            tokenSource = new();
            clearTask = Task.Run(async () =>
            {
                await Task.Yield();
                var q = queue;
                while (!tokenSource.IsCancellationRequested)
                {
                    for (var i = 0; i < 1024; i++)
                    {
                        q.TryDequeue(out _);
                    }
                }
            });
        }

        [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
        public void BoundedNativeRingQueue()
        {
            var q = queue;
            ArgumentNullException.ThrowIfNull(q);
            var w = q.GetWriter();
            for (var i = 0; i < OperationsPerInvoke; i++)
            {
                while (!w.TryAdd(i))
                {
                    // try again
                }
            }
        }

        [GlobalCleanup(Target = nameof(BoundedNativeRingQueue))]
        public void CleanupBoundedNativeRingQueue()
        {
            tokenSource?.Cancel();
            clearTask?.Wait();
            queue?.Dispose();
            tokenSource?.Dispose();
        }
    }
}
