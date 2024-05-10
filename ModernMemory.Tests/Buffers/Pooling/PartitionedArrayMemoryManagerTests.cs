using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Buffers.Pooling;

namespace ModernMemory.Tests.Buffers.Pooling
{
    [TestFixture]
    public class PartitionedArrayMemoryManagerTests
    {
        [Test]
        public void TryAllocateAllocatesCorrectly()
        {
            using var manager = new SharedNativeMemoryPool<byte>.PartitionedArrayMemoryManager<FixedArray32<uint>>(2048);
            var m = manager.TryAllocate(out var memory);
            Assert.Multiple(() =>
            {
                Assert.That(m, Is.True);
                Assert.That(memory, Is.LessThan(nuint.MaxValue));
            });
        }

        [Test]
        public void TryAllocateFailsCorrectlyWhenFull()
        {
            using var manager = new SharedNativeMemoryPool<byte>.PartitionedArrayMemoryManager<FixedArray32<uint>>(2048);
            var of = manager.MutableOccupationFlags;
            of.Fill(~0u);
            var m = manager.TryAllocate(out var memory);
            Assert.Multiple(() =>
            {
                Assert.That(m, Is.False);
                Assert.That(memory, Is.EqualTo(nuint.MaxValue));
            });
        }

        [Test]
        public void ReturnReturnsCorrectly()
        {
            using var manager = new SharedNativeMemoryPool<byte>.PartitionedArrayMemoryManager<FixedArray32<uint>>(2048);
            var m = manager.TryAllocate(out var memory);
            manager.Return(memory);
            Assert.Multiple(() =>
            {
                Assert.That(m, Is.True);
                Assert.That(memory, Is.LessThan(nuint.MaxValue));
            });
        }

        [Test]
        public void ReturnThrowsCorrectlyIfAlreadyFree()
        {
            using var manager = new SharedNativeMemoryPool<byte>.PartitionedArrayMemoryManager<FixedArray32<uint>>(2048);
            
            Assert.Throws(Is.TypeOf<InvalidOperationException>(), () => manager.Return(0));
        }

        private static IEnumerable<TestCaseData> ATestCaseSource() => 
        [
            new(nuint.MaxValue),
            ..Enumerable.Range(0, 64).Select(a => (nuint)1 << a).SelectMany<nuint, nuint>(a => [a, a * 3, a * 5, a * 7])
            .SelectMany<nuint, nuint>(a => [a, a + 1, a - 1]).Distinct().Except([(nuint)0]).Select(a => new TestCaseData(a)),
        ];

        [TestCaseSource(nameof(ATestCaseSource))]
        public void A(nuint v)
        {
            var res = BufferUtils.CalculatePartitionSizeClassIndex(v, out var sizeClass);
            Assert.That(sizeClass, Is.GreaterThanOrEqualTo(v));
            var sv = BufferUtils.CalculatePartitionSizeClassFromIndex(res);
            Assert.That(sv, Is.EqualTo(sizeClass));
        }
    }
}
