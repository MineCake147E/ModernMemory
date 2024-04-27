using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Sorting;
using ModernMemory.Utils;

namespace ModernMemory.Tests.Sorting
{
    [TestFixture]
    public partial class BinaryInsertionSortTests
    {
        [TestCase(0, 1, 2, 3, 4, 5, 6, 7)]
        [TestCase(7, 6, 5, 4, 3, 2, 1, 0)]
        [TestCase(27, 5, 28, 15, 3, 10, 11, 25, 13, 24, 21, 23, 22, 6, 16, 26, 0, 18, 19, 1, 31, 4, 14, 2, 29, 8, 9, 12, 30, 7, 17, 20)]
        public void SortSortsCorrectly(params int[] elements)
        {
            var exp = elements.AsSpan();
            var copy = exp.ToArray();
            var act = copy.AsNativeSpan();
            exp.Sort();
            BinaryInsertionSort.Sort(act);
            Assert.That(copy, Is.EqualTo(elements));
        }

        [TestCase(0, 1, 2, 3, 4, 5, 6, 7)]
        [TestCase(7, 6, 5, 4, 3, 2, 1, 0)]
        [TestCase(3, 3, 3, 5, 1, 1, 7, 4, 1, 6, 2, 2, 6, 3, 5, 5, 0, 7, 4, 0, 7, 5, 2, 1, 6, 4, 0, 6, 0, 4, 7, 2)]
        [TestCase(2, 11, 6, 10, 31, 0, 24, 12, 10, 23, 31, 25, 22, 8, 5, 17, 21, 3, 4, 7, 14, 3, 6, 15, 12, 29, 2, 30, 22, 1, 11, 27, 0, 9, 20, 9, 13, 25, 5, 28, 16, 16, 26, 23, 30, 27, 7, 17, 13, 1, 15, 24, 28, 18, 26, 4, 20, 18, 19, 19, 21, 29, 8, 14)]
        public void SortStablySortsCorrectly(params int[] eSource)
        {
            var elements = eSource.Select((a, i) => ((ulong)a << 32) | (uint)i);
            var copy = elements.ToArray().AsSpan().ToArray();
            var exp = elements.Order().ToArray();
            var act = copy.AsNativeSpan();
            BinaryInsertionSort.Sort<ulong, TransformedStaticComparer<ulong, uint, ComparisonOperatorsStaticComparer<uint>, BitShiftTransform>>(act);
            Assert.That(copy, Is.EqualTo(exp));
        }
    }
}
