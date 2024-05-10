using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections;
using ModernMemory.DataFlow;

namespace ModernMemory.Tests.Collections
{
    [TestFixture]
    public class NativePileTests
    {
        [TestCase(53, 12)]
        public void PurgePurgesCorrectlyThreshold(int elements, int threshold)
        {
            using NativePile<int> pile = [.. Enumerable.Range(0, elements)];
            pile.PurgeItemsBy(a => a >= threshold);
            Assert.That(pile, Is.EqualTo(Enumerable.Range(0, threshold)));
        }

        [TestCase(53, 12, 6)]
        public void PurgePurgesCorrectlyThresholdLimited(int elements, int threshold, int limit)
        {
            using NativePile<int> pile = [.. Enumerable.Range(0, elements)];
            var m = new int[limit];
            var writer = m.AsNativeSpan().AsDataWriter();
            var count = pile.PurgeItemsBy(a => a >= threshold, ref writer);
            writer.Dispose();
            Assert.That(count, Is.LessThanOrEqualTo((nuint)limit));
        }

        [TestCase(53, 12)]
        public void PurgePurgesCorrectlyThresholdReverse(int elements, int threshold)
        {
            using NativePile<int> pile = [.. Enumerable.Range(0, elements)];
            pile.PurgeItemsBy(a => a < threshold);
            Assert.That(pile, Is.EquivalentTo(Enumerable.Range(threshold, elements - threshold)));
        }

        [TestCase(53, 12, 6)]
        public void PurgePurgesCorrectlyThresholdReverseLimited(int elements, int threshold, int limit)
        {
            using NativePile<int> pile = [.. Enumerable.Range(0, elements)];
            var m = new int[limit];
            var writer = m.AsNativeSpan().AsDataWriter();
            var count = pile.PurgeItemsBy(a => a < threshold, ref writer);
            writer.Dispose();
            Assert.That(count, Is.LessThanOrEqualTo((nuint)limit));
        }

        [TestCase(53)]
        public void PurgePurgesCorrectlyLSB(int elements)
        {
            using NativePile<int> pile = [.. Enumerable.Range(0, elements)];
            pile.PurgeItemsBy(a => (a & 1) > 0);
            Assert.That(pile, Is.EquivalentTo(Enumerable.Range(0, elements).Where(a => (a & 1) == 0)));
        }

        [TestCase(53, 6)]
        public void PurgePurgesCorrectlyLSBLimited(int elements, int limit)
        {
            using NativePile<int> pile = [.. Enumerable.Range(0, elements)];
            var m = new int[limit];
            var writer = m.AsNativeSpan().AsDataWriter();
            var count = pile.PurgeItemsBy(a => (a & 1) > 0, ref writer);
            writer.Dispose();
            Assert.That(count, Is.LessThanOrEqualTo((nuint)limit));
        }
    }
}
