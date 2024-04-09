using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Sorting;
using ModernMemory.Utils;

namespace ModernMemory.Tests.Sorting
{
    [TestFixture]
    public class ShellSortTests
    {
        private static IEnumerable<int> Sizes => [1, 2, 3, 4, 5, 9, 10, 11, 22, 23, 24, 56, 57, 58, 131, 132, 133, 300, 301, 302, 700, 701, 702, 1749, 1750, 1751, 1048576, 16777216];

        private static IEnumerable<TestCaseData> SortLengthTestCaseSource => Sizes.Select(x => new TestCaseData(x));

        [NonParallelizable]
        [TestCaseSource(nameof(SortLengthTestCaseSource))]
        public void SortByStaticProxySortsCorrectly(int size)
        {
            var values = new int[size];
            var vs = values.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(vs));
            var sw = Stopwatch.StartNew();
            Console.WriteLine("Sorting Start!");
            sw.Restart();
            ShellSort.SortByStaticProxy<int, ComparableStaticComparisonProxy<int>>(vs.AsNativeSpan());
            sw.Stop();
            Console.WriteLine($"Sorting took {sw.Elapsed}");
            Assert.That(values, Is.Ordered);
        }
    }
}
