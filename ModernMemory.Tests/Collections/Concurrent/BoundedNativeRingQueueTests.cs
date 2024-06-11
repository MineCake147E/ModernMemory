using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections;
using ModernMemory.Collections.Concurrent;
using ModernMemory.Collections.Storage;
using ModernMemory.DataFlow;
using ModernMemory.Threading;

namespace ModernMemory.Tests.Collections.Concurrent
{
    [TestFixture]
    public class BoundedNativeRingQueueTests
    {
        [Test]
        public void AddSingleAddsCorrectly()
        {
            using var q = BoundedNativeRingQueue.Create<int>(2);
            q.TryAdd(1);
            Assert.That(q.Peek(), Is.EqualTo(1));
        }

        [Test]
        public void AddMultipleAddsCorrectly()
        {
#pragma warning disable IDE0028 // Simplify collection initialization
            using var nq = BoundedNativeRingQueue.Create<int>(64);
            nq.AddAtMost([.. Enumerable.Range(0, 32)]);
#pragma warning restore IDE0028 // Simplify collection initialization
            Assert.That(nq, Is.EqualTo(Enumerable.Range(0, 32)));
        }

        [Test]
        public void CollectionInitializerSingleInitializesCorrectly()
        {
            using var nq = BoundedNativeRingQueue.Create([1]);
            Assert.That(nq.Peek(), Is.EqualTo(1));
        }

        [Test]
        public void CollectionInitializerMultipleInitializesCorrectly()
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, 32)]);
            Assert.That(nq, Is.EqualTo(Enumerable.Range(0, 32)));
        }

        [Test]
        public void ClearClearsCorrectly() => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, 32)]);
            nq.Clear();
            Assert.That(nq.Count, Is.EqualTo(nuint.MinValue));
            Assert.That(nq, Is.Empty);
        });

        #region Dequeue

        [Test]
        public void DequeueSingleDequeuesCorrectly() => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, 32)]);
            _ = nq.Dequeue();
            Assert.That(nq.Count, Is.EqualTo((nuint)31));
            Assert.That(nq, Is.EqualTo(Enumerable.Range(1, 31)));
        });

        [Test]
        public void DequeueSingleThrowsCorrectlyIfEmpty()
        {
            using var nq = BoundedNativeRingQueue.Create<int>([]);
            Assert.Throws<InvalidOperationException>(() => _ = nq.Dequeue());
        }

        [Test]
        public void TryDequeueSingleDequeuesCorrectly() => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, 32)]);
            var res = nq.TryDequeue(out var item);
            Assert.That(item, Is.Zero);
            Assert.That(res, Is.True);
            Assert.That(nq.Count, Is.EqualTo((nuint)31));
            Assert.That(nq, Is.EqualTo(Enumerable.Range(1, 31)));
        });

        [Test]
        public void TryDequeueSingleDoesNotThrow() => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create<int>([]);
            var item = -1;
            var res = false;
            Assert.DoesNotThrow(() => res = nq.TryDequeue(out item));
            Assert.That(res, Is.False);
            Assert.That(item, Is.EqualTo(-1));
            Assert.That(nq, Is.Empty);
        });

        [TestCase(32), TestCase(1048576)]
        public void DequeueAllDequeuesCorrectly(int size) => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var abw = new ArrayBufferWriter<int>();
            nq.DequeueAll(ref abw);
            Assert.That(nq.Count, Is.EqualTo((nuint)0));
            Assert.That(nq, Is.Empty);
            Assert.That(abw.WrittenSpan.ToArray(), Is.EqualTo(Enumerable.Range(0, size)));
        });

        [Test]
        public void DequeueAllDoesNotThrowIfEmpty()
        {
            using var nq = BoundedNativeRingQueue.Create<int>([]);
            var abw = new ArrayBufferWriter<int>();
            Assert.DoesNotThrow(() => nq.DequeueAll(ref abw));
        }

        #region DequeueRange

        [TestCase(32, 16), TestCase(1048576, 524288)]
        public void DequeueRangeDataWriterConstrainedBufferWriterDequeuesCorrectly(int size, int dequeueCount) => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var abw = new ArrayBufferWriter<int>(dequeueCount);
            var dw = DataWriter.CreateFrom(ref abw, (nuint)dequeueCount);
            try
            {
                nq.DequeueRange(ref dw);
                dw.Flush();
                Assert.That(nq.Count, Is.EqualTo((nuint)(size - dequeueCount)));
                Assert.That(nq, Is.EqualTo(Enumerable.Range(dequeueCount, size - dequeueCount)));
                Assert.That(abw.WrittenSpan.ToArray(), Is.EqualTo(Enumerable.Range(0, dequeueCount)));
            }
            finally
            {
                dw.Dispose();
            }
        });

        [TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DequeueRangeDataWriterConstrainedBufferWriterThrowsCorrectlyInsufficientItems(int size, int dequeueCount)
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var abw = new ArrayBufferWriter<int>(dequeueCount);
            Assert.Throws<InvalidOperationException>(() =>
            {
                var dw = DataWriter.CreateFrom(ref abw, (nuint)dequeueCount);
                try
                {
                    nq.DequeueRange(ref dw);
                }
                finally
                {
                    dw.Dispose();
                }
            });
        }

        [TestCase(32), TestCase(1048576)]
        public void DequeueRangeDataWriterUnconstrainedBufferWriterDequeuesCorrectly(int size) => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var abw = new ArrayBufferWriter<int>();
            Assert.DoesNotThrow(() =>
            {
                var dw = DataWriter.CreateFrom(ref abw);
                try
                {
                    nq.DequeueRange(ref dw);
                    dw.Flush();
                }
                finally
                {
                    dw.Dispose();
                }
            });
            Assert.That(nq.Count, Is.EqualTo((nuint)0));
            Assert.That(nq, Is.Empty);
            Assert.That(abw.WrittenSpan.ToArray(), Is.EqualTo(Enumerable.Range(0, size)));
        });

        [TestCase(32, 16), TestCase(1048576, 524288)]
        public void DequeueRangeDataWriterNativeSpanDequeuesCorrectly(int size, int dequeueCount) => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var arr = new int[dequeueCount];
            var dw = DataWriter.CreateFrom(arr);
            try
            {
                nq.DequeueRange(ref dw);
                dw.Flush();
                Assert.That(nq.Count, Is.EqualTo((nuint)(size - dequeueCount)));
                Assert.That(nq, Is.EqualTo(Enumerable.Range(dequeueCount, size - dequeueCount)));
                Assert.That(arr, Is.EqualTo(Enumerable.Range(0, dequeueCount)));
            }
            finally
            {
                dw.Dispose();
            }
        });

        [TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DequeueRangeDataWriterNativeSpanThrowsCorrectlyInsufficientItems(int size, int dequeueCount)
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var arr = new int[dequeueCount];
            Assert.Throws<InvalidOperationException>(() =>
            {
                var dw = DataWriter.CreateFrom(arr);
                try
                {
                    nq.DequeueRange(ref dw);
                }
                finally
                {
                    dw.Dispose();
                }
            });
        }

        #endregion

        [TestCase(32, 16), TestCase(1048576, 524288)]
        public void DequeueRangeExactNativeSpanDequeuesCorrectly(int size, int dequeueCount) => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var arr = new int[dequeueCount];
            nq.DequeueRangeExact(arr);
            Assert.That(nq.Count, Is.EqualTo((nuint)(size - dequeueCount)));
            Assert.That(nq, Is.EqualTo(Enumerable.Range(dequeueCount, size - dequeueCount)));
            Assert.That(arr, Is.EqualTo(Enumerable.Range(0, dequeueCount)));
        });

        [TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DequeueRangeExactNativeSpanThrowsCorrectlyInsufficientItems(int size, int dequeueCount)
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var arr = new int[dequeueCount];
            Assert.Throws<InvalidOperationException>(() => nq.DequeueRangeExact(arr));
        }

        #region DequeueRangeAtMost

        [TestCase(32, 16), TestCase(1048576, 524288), TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DequeueRangeAtMostDataWriterConstrainedBufferWriterDequeuesCorrectly(int size, int dequeueCount) => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var abw = new ArrayBufferWriter<int>(dequeueCount);
            var count = nuint.MaxValue;
            Assert.DoesNotThrow(() =>
            {
                var dw = DataWriter.CreateFrom(ref abw, (nuint)dequeueCount);
                try
                {
                    count = nq.DequeueRangeAtMost(ref dw);
                    dw.Flush();
                }
                finally
                {
                    dw.Dispose();
                }
            });
            var expectedCount = nuint.Min((nuint)dequeueCount, (nuint)size);
            Assert.That(count, Is.EqualTo(expectedCount));
            Assert.That(nq.Count, Is.EqualTo((nuint)size - expectedCount));
            Assert.That(nq, Is.EqualTo(Enumerable.Range((int)expectedCount, size - (int)expectedCount)));
            Assert.That(abw.WrittenSpan.ToArray(), Is.EqualTo(Enumerable.Range(0, (int)expectedCount)));
        });

        [TestCase(32), TestCase(1048576)]
        public void DequeueRangeAtMostDataWriterUnconstrainedBufferWriterDequeuesCorrectly(int size) => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var abw = new ArrayBufferWriter<int>();
            Assert.DoesNotThrow(() =>
            {
                var dw = DataWriter.CreateFrom(ref abw);
                try
                {
                    nq.DequeueRangeAtMost(ref dw);
                    dw.Flush();
                }
                finally
                {
                    dw.Dispose();
                }
            });
            Assert.That(nq.Count, Is.EqualTo((nuint)0));
            Assert.That(nq, Is.Empty);
            Assert.That(abw.WrittenSpan.ToArray(), Is.EqualTo(Enumerable.Range(0, size)));
        });

        [TestCase(32, 16), TestCase(1048576, 524288), TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DequeueRangeAtMostDataWriterNativeSpanDequeuesCorrectly(int size, int dequeueCount) => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var arr = new int[dequeueCount];
            var count = nuint.MaxValue;
            Assert.DoesNotThrow(() =>
            {
                var dw = DataWriter.CreateFrom(arr);
                try
                {
                    count = nq.DequeueRangeAtMost(ref dw);
                    dw.Flush();
                }
                finally
                {
                    dw.Dispose();
                }
            });
            var expectedCount = nuint.Min((nuint)dequeueCount, (nuint)size);
            Assert.That(count, Is.EqualTo(expectedCount));
            Assert.That(nq.Count, Is.EqualTo((nuint)size - expectedCount));
            Assert.That(nq, Is.EqualTo(Enumerable.Range((int)expectedCount, size - (int)expectedCount)));
            Assert.That(arr.AsSpan(0, (int)expectedCount).ToArray(), Is.EqualTo(Enumerable.Range(0, (int)expectedCount)));
        });

        [TestCase(32, 16), TestCase(1048576, 524288), TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DequeueRangeAtMostNativeSpanDequeuesCorrectly(int size, int dequeueCount) => Assert.Multiple(() =>
        {
            using var nq = BoundedNativeRingQueue.Create([.. Enumerable.Range(0, size)]);
            var arr = new int[dequeueCount];
            var count = nuint.MaxValue;
            Assert.DoesNotThrow(() => count = nq.DequeueRangeAtMost(arr.AsNativeSpan()));
            var expectedCount = nuint.Min((nuint)dequeueCount, (nuint)size);
            Assert.That(count, Is.EqualTo(expectedCount));
            Assert.That(nq.Count, Is.EqualTo((nuint)size - expectedCount));
            Assert.That(nq, Is.EqualTo(Enumerable.Range((int)expectedCount, size - (int)expectedCount)));
            Assert.That(arr.AsSpan(0, (int)expectedCount).ToArray(), Is.EqualTo(Enumerable.Range(0, (int)expectedCount)));
        });

        #endregion

        #endregion

        #region Concurrent
        [TestCase(256, 256)]
        [TestCase(256, 16)]
        [TestCase(1 << 24, 65535)]
        [TestCase(1 << 24, 16)]
        public void AddAndDequeueSingleConcurrentlyAddsAndDequeuesCorrectly(int count, int capacity)
        {
            using var bnq = BoundedNativeRingQueue.Create<int>(capacity);
            var nq = new NativeQueue<int>((nuint)count);
            using var e = new SemaphoreSlim(0, 1);
            using var mre = new ManualResetEventSlim(false);
            var t = Task.Run(async () =>
            {
                var sw = new Stopwatch();
                await Task.Yield();
                await e.WaitAsync().ConfigureAwait(false);
                sw.Start();
                for (int i = 0; i < count; i++)
                {
                    while (!bnq.TryAdd(i))
                    {
                        //
                    }
                }
                sw.Stop();
                Console.WriteLine($"Enqueuing {count} items took {sw.ElapsedMilliseconds} [ms] ({count / sw.Elapsed.TotalSeconds} items/s)");
            });
            var sw = new Stopwatch();
            e.Release();
            sw.Start();
            while (nq.Count < (nuint)count)
            {
                if (bnq.TryDequeue(out var item))
                {
                    nq.Add(item);
                }
            }
            sw.Stop();
            Console.WriteLine($"Dequeuing {count} items took {sw.ElapsedMilliseconds} [ms] ({count / sw.Elapsed.TotalSeconds} items/s)");
            t.Wait();
            
            Assert.That(nq, Is.EqualTo((IEnumerable<int>?)Enumerable.Range(0, count)));
            nq.Dispose();
        }
        #endregion
    }
}
