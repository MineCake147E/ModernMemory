using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Collections;
using ModernMemory.DataFlow;

namespace ModernMemory.Tests.Collections
{
    [TestFixture]
    public class NativeQueueTests
    {
        [Test]
        public void AddSingleAddsCorrectly()
        {
#pragma warning disable IDE0028 // Simplify collection initialization
            using var nq = new NativeQueue<int>();
            nq.Add(1);
#pragma warning restore IDE0028 // Simplify collection initialization
            Assert.That(nq.Peek(), Is.EqualTo(1));
        }

        [Test]
        public void AddMultipleAddsCorrectly()
        {
#pragma warning disable IDE0028 // Simplify collection initialization
            using var nq = new NativeQueue<int>();
            nq.Add([.. Enumerable.Range(0, 32)]);
#pragma warning restore IDE0028 // Simplify collection initialization
            Assert.That(nq, Is.EqualTo(Enumerable.Range(0, 32)));
        }

        [Test]
        public void CollectionInitializerSingleInitializesCorrectly()
        {
            using NativeQueue<int> nq = [1];
            Assert.That(nq.Peek(), Is.EqualTo(1));
        }

        [Test]
        public void CollectionInitializerMultipleInitializesCorrectly()
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, 32)];
            Assert.That(nq, Is.EqualTo(Enumerable.Range(0, 32)));
        }

        [Test]
        public void ClearClearsCorrectly() => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, 32)];
            nq.Clear();
            Assert.That(nq.Count, Is.EqualTo(nuint.MinValue));
            Assert.That(nq, Is.Empty);
        });

        #region Dequeue

        [Test]
        public void DequeueSingleDequeuesCorrectly() => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, 32)];
            _ = nq.Dequeue();
            Assert.That(nq.Count, Is.EqualTo((nuint)31));
            Assert.That(nq, Is.EqualTo(Enumerable.Range(1, 31)));
        });

        [Test]
        public void DequeueSingleThrowsCorrectlyIfEmpty()
        {
            using NativeQueue<int> nq = [];
            Assert.Throws<InvalidOperationException>(() => _ = nq.Dequeue());
        }

        [Test]
        public void TryDequeueSingleDequeuesCorrectly() => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, 32)];
            var res = nq.TryDequeue(out var item);
            Assert.That(item, Is.Zero);
            Assert.That(res, Is.True);
            Assert.That(nq.Count, Is.EqualTo((nuint)31));
            Assert.That(nq, Is.EqualTo(Enumerable.Range(1, 31)));
        });

        [Test]
        public void TryDequeueSingleDoesNotThrow() => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [];
            int item = -1;
            bool res = false;
            Assert.DoesNotThrow(() => res = nq.TryDequeue(out item));
            Assert.That(res, Is.False);
            Assert.That(item, Is.EqualTo(-1));
            Assert.That(nq, Is.Empty);
        });

        [TestCase(32), TestCase(1048576)]
        public void DequeueAllDequeuesCorrectly(int size) => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
            var abw = new ArrayBufferWriter<int>();
            nq.DequeueAll(ref abw);
            Assert.That(nq.Count, Is.EqualTo((nuint)0));
            Assert.That(nq, Is.Empty);
            Assert.That(abw.WrittenSpan.ToArray(), Is.EqualTo(Enumerable.Range(0, size)));
        });

        [Test]
        public void DequeueAllDoesNotThrowIfEmpty()
        {
            using NativeQueue<int> nq = [];
            var abw = new ArrayBufferWriter<int>();
            Assert.DoesNotThrow(() => nq.DequeueAll(ref abw));
        }

        #region DequeueRange

        [TestCase(32, 16), TestCase(1048576, 524288)]
        public void DequeueRangeDataWriterConstrainedBufferWriterDequeuesCorrectly(int size, int dequeueCount) => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
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
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
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
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
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
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
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
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
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
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
            var arr = new int[dequeueCount];
            nq.DequeueRangeExact(arr);
            Assert.That(nq.Count, Is.EqualTo((nuint)(size - dequeueCount)));
            Assert.That(nq, Is.EqualTo(Enumerable.Range(dequeueCount, size - dequeueCount)));
            Assert.That(arr, Is.EqualTo(Enumerable.Range(0, dequeueCount)));
        });

        [TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DequeueRangeExactNativeSpanThrowsCorrectlyInsufficientItems(int size, int dequeueCount)
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
            var arr = new int[dequeueCount];
            Assert.Throws<ArgumentOutOfRangeException>(() => nq.DequeueRangeExact(arr));
        }

        #region DequeueRangeAtMost

        [TestCase(32, 16), TestCase(1048576, 524288), TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DequeueRangeAtMostDataWriterConstrainedBufferWriterDequeuesCorrectly(int size, int dequeueCount) => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
            var abw = new ArrayBufferWriter<int>(dequeueCount);
            nuint count = nuint.MaxValue;
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
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
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
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
            var arr = new int[dequeueCount];
            nuint count = nuint.MaxValue;
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
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
            var arr = new int[dequeueCount];
            nuint count = nuint.MaxValue;
            Assert.DoesNotThrow(() => count = nq.DequeueRangeAtMost(arr.AsNativeSpan()));
            var expectedCount = nuint.Min((nuint)dequeueCount, (nuint)size);
            Assert.That(count, Is.EqualTo(expectedCount));
            Assert.That(nq.Count, Is.EqualTo((nuint)size - expectedCount));
            Assert.That(nq, Is.EqualTo(Enumerable.Range((int)expectedCount, size - (int)expectedCount)));
            Assert.That(arr.AsSpan(0, (int)expectedCount).ToArray(), Is.EqualTo(Enumerable.Range(0, (int)expectedCount)));
        });

        #endregion

        #endregion

        [TestCase(32, 16), TestCase(1048576, 524288)]
        public void DiscardHeadDiscardsCorrectly(int size, int discardCount)
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
            nq.DiscardHead((nuint)discardCount);
            Assert.That(nq, Is.EqualTo(Enumerable.Range(discardCount, size - discardCount)));
        }

        [TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DiscardHeadThrowsCorrectlyInsufficientItems(int size, int discardCount)
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
            Assert.Throws<ArgumentOutOfRangeException>(() => nq.DiscardHead((nuint)discardCount));
        }

        [TestCase(32, 16), TestCase(1048576, 524288), TestCase(0, 1), TestCase(16, 32), TestCase(524288, 1048576)]
        public void DiscardHeadAtMostDiscardsCorrectly(int size, int discardCount)
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, size)];
            Assert.Multiple(() =>
            {
                nuint count = nuint.MaxValue;
                Assert.DoesNotThrow(() => count = nq.DiscardHeadAtMost((nuint)discardCount));
                var expectedCount = int.Min(discardCount, size);
                Assert.That(count, Is.EqualTo((nuint)expectedCount));
                Assert.That(nq, Is.EqualTo(Enumerable.Range(expectedCount, size - expectedCount)));
            });
        }

        [TestCase(32, 16u), TestCase(1048576, 524288u), TestCase(0, 1u), TestCase(16, 32u), TestCase(524288, 1048576u), TestCase(0, ~0u)]
        public void EnsureCapacityToAddExpandsCorrectly(int initialSize, uint newSize)
        {
            var nq = new NativeQueueCore<FixedArray256<Guid>>((nuint)initialSize);
            Assert.Multiple(() =>
            {
                var e = Assert.Throws(Is.Null.Or.TypeOf<OutOfMemoryException>(), () => nq.EnsureCapacityToAdd(newSize));
                if (e is { } e2)
                {
                    Console.WriteLine($"EnsureCapacityToAdd resulted in {e2.GetType()}.{Environment.NewLine}{e2}");
                }
                else
                {
                    Assert.That(nq.Writable.Length, Is.GreaterThanOrEqualTo((nuint)newSize));
                }
            });
        }

        #region Peek

        [Test]
        public void PeekSinglePeeksCorrectly() => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, 32)];
            var item = nq.Peek();
            Assert.That(item, Is.Zero);
            Assert.That(nq.Count, Is.EqualTo((nuint)32));
            Assert.That(nq, Is.EqualTo(Enumerable.Range(0, 32)));
        });

        [Test]
        public void PeekSingleThrowsCorrectlyIfEmpty()
        {
            using NativeQueue<int> nq = [];
            Assert.Throws<InvalidOperationException>(() => _ = nq.Peek());
        }

        [Test]
        public void TryPeekSinglePeeksCorrectly() => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [.. Enumerable.Range(0, 32)];
            var res = nq.TryPeek(out var item);
            Assert.That(item, Is.Zero);
            Assert.That(res, Is.True);
            Assert.That(nq.Count, Is.EqualTo((nuint)32));
            Assert.That(nq, Is.EqualTo(Enumerable.Range(0, 32)));
        });

        [Test]
        public void TryPeekSingleDoesNotThrow() => Assert.Multiple(() =>
        {
            using NativeQueue<int> nq = [];
            int item = -1;
            bool res = false;
            Assert.DoesNotThrow(() => res = nq.TryPeek(out item));
            Assert.That(res, Is.False);
            Assert.That(item, Is.EqualTo(-1));
            Assert.That(nq, Is.Empty);
        });

        #endregion
    }
}
