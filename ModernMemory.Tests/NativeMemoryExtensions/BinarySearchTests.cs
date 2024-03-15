using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Tests.NativeMemoryExtensions
{
    [TestFixture]
    public class BinarySearchTests
    {

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(32767)]
        [TestCase(32768)]
        [TestCase(32769)]
        [TestCase(65535)]
        [TestCase(65536)]
        [TestCase(65537)]
        public void BinarySearchComparisonOperatorsFindsCorrectlyEvenLength(int value)
        {
            int[] values = Enumerable.Range(0, 32768).Select(a => a * 2).ToArray();
            var span = values.AsNativeSpan();
            var index = (int)span.BinarySearchComparisonOperators(value, out var exactMatch);
            var expectedIndex = span.GetHeadSpan().BinarySearch(value);
            Assert.Multiple(() =>
            {
                Assert.That(exactMatch, Is.EqualTo(expectedIndex >= 0));
                Assert.That(index, Is.EqualTo(expectedIndex ^ (expectedIndex >> 31)));
            });
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(32765)]
        [TestCase(32766)]
        [TestCase(32767)]
        [TestCase(65531)]
        [TestCase(65532)]
        [TestCase(65533)]
        public void BinarySearchComparisonOperatorsFindsCorrectlyOddLength(int value)
        {
            int[] values = Enumerable.Range(0, 32767).Select(a => a * 2).ToArray();
            var span = values.AsNativeSpan();
            var index = (int)span.BinarySearchComparisonOperators(value, out var exactMatch);
            var expectedIndex = span.GetHeadSpan().BinarySearch(value);
            Assert.Multiple(() =>
            {
                Assert.That(exactMatch, Is.EqualTo(expectedIndex >= 0));
                Assert.That(index, Is.EqualTo(expectedIndex ^ (expectedIndex >> 31)));
            });
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(32767)]
        [TestCase(32768)]
        [TestCase(32769)]
        [TestCase(65535)]
        [TestCase(65536)]
        [TestCase(65537)]
        public void BinarySearchComparableFindsCorrectlyEvenLength(int value)
        {
            int[] values = Enumerable.Range(0, 32768).Select(a => a * 2).ToArray();
            var span = values.AsNativeSpan();
            var index = (int)span.BinarySearchComparable(value, out var exactMatch);
            var expectedIndex = span.GetHeadSpan().BinarySearch(value);
            Assert.Multiple(() =>
            {
                Assert.That(exactMatch, Is.EqualTo(expectedIndex >= 0));
                Assert.That(index, Is.EqualTo(expectedIndex ^ (expectedIndex >> 31)));
            });
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(32765)]
        [TestCase(32766)]
        [TestCase(32767)]
        [TestCase(65531)]
        [TestCase(65532)]
        [TestCase(65533)]
        public void BinarySearchComparableFindsCorrectlyOddLength(int value)
        {
            int[] values = Enumerable.Range(0, 32767).Select(a => a * 2).ToArray();
            var span = values.AsNativeSpan();
            var index = (int)span.BinarySearchComparable(value, out var exactMatch);
            var expectedIndex = span.GetHeadSpan().BinarySearch(value);
            Assert.Multiple(() =>
            {
                Assert.That(exactMatch, Is.EqualTo(expectedIndex >= 0));
                Assert.That(index, Is.EqualTo(expectedIndex ^ (expectedIndex >> 31)));
            });
        }
    }
}
