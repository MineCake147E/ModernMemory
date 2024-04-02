using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Sorting;
using ModernMemory.Utils;

namespace ModernMemory.Tests.Sorting
{
    [TestFixture]
    public class AdaptiveOptimizedGrailSortTests
    {
        private static IEnumerable<int> GenerateAllDistinctModulusValues(int max)
            => Enumerable.Range(1, max - 1).DistinctBy(a => max % a).SelectMany<int, int>(a => [a, max - a]).Distinct();
        private static IEnumerable<(int size, int boundary)> GenerateSizeAndBoundaryValues()
            => [(2048, 1), (2048, 2047), (2048, 67), (2048, 2048 - 67), (2048, 768),
                //..GenerateAllDistinctModulusValues(128).Select(a => (128, a)),
                ..GenerateAllDistinctModulusValues(32).Select(a => (32, a)),
                ..GenerateAllDistinctModulusValues(16).Select(a => (16, a)),];

        private static IEnumerable<TestCaseData> SizeAndBoundaryTestCaseSource()
            => GenerateSizeAndBoundaryValues().Select(a => new TestCaseData(a.size, a.boundary));

        private static IEnumerable<int> BufferExtraSizeValues()
            => [0, 1, 63, 64];

        private static IEnumerable<TestCaseData> SizeBoundaryBufferExtraSizeTestCaseSource()
            => GenerateSizeAndBoundaryValues().SelectMany(a => BufferExtraSizeValues().Select(b => new TestCaseData(a.size, a.boundary, b)));

        private static IEnumerable<TestCaseData> MergeTestCaseSource()
            => GenerateSizeAndBoundaryValues().SelectMany(a => BufferExtraSizeValues().SelectMany<int, TestCaseData>(b =>
            [
                new TestCaseData(a.size, a.boundary, b, null) { TypeArgs = [typeof(IdentityPermutationProvider<object>), typeof(object)] },
                new TestCaseData(a.size, a.boundary, b, null) { TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)] },
                new TestCaseData(a.size, a.boundary, b, a.boundary) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
            ]));

        [TestCaseSource(nameof(SizeAndBoundaryTestCaseSource))]
        public void RotateRotatesValuesCorrectly(int size, int boundary)
        {
            var guard = 512;
            var exp = new int[size + guard * 2];
            var act = new int[exp.Length];
            var sep = exp.AsSpan();
            var sap = act.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(sap));
            sap.CopyTo(sep);
            var sa = sap.Slice(guard, size);
            var se = sep.Slice(guard, size);
            for (int i = 0; i < sa.Length; i++)
            {
                sa[i] = i;
            }
            var f = sa.Slice(boundary);
            f.CopyTo(se);
            sa.Slice(0, boundary).CopyTo(se.Slice(f.Length));
            AdaptiveOptimizedGrailSort.Rotate(ref MemoryMarshal.GetReference(sa), (nuint)boundary, (nuint)(sa.Length - boundary));
            Assert.That(act, Is.EqualTo(exp));
        }

        [TestCaseSource(nameof(SizeAndBoundaryTestCaseSource))]
        public void RotateRotatesReferencesCorrectly(int size, int boundary)
        {
            var guard = 512;
            var exp = new StrongBox<int>[size + guard * 2];
            var act = new StrongBox<int>[exp.Length];
            var dummy = new StrongBox<int>(exp.Length);
            var sep = exp.AsSpan();
            var sap = act.AsSpan();
            sap.Fill(dummy);
            sap.CopyTo(sep);
            var sa = sap.Slice(guard, size);
            var se = sep.Slice(guard, size);
            for (int i = 0; i < sa.Length; i++)
            {
                sa[i] = new(i);
            }
            var f = sa.Slice(boundary);
            f.CopyTo(se);
            sa.Slice(0, boundary).CopyTo(se.Slice(f.Length));
            AdaptiveOptimizedGrailSort.RotateReferences(ref MemoryMarshal.GetReference(sa), (nuint)boundary, (nuint)(sa.Length - boundary));
            Assert.That(act.Select(a => a.Value).ToList(), Is.EqualTo(exp.Select(a => a.Value).ToList()));
        }

        [TestCaseSource(nameof(SizeAndBoundaryTestCaseSource))]
        public void InsertBufferForwardsUnorderedInsertsValuesCorrectly(int size, int boundary)
        {
            var guard = 512;
            var exp = new int[size + guard * 2];
            var act = new int[exp.Length];
            var sep = exp.AsSpan();
            var sap = act.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(sap));
            sap.CopyTo(sep);
            var sa = sap.Slice(guard, size);
            var se = sep.Slice(guard, size);
            for (int i = 0; i < sa.Length; i++)
            {
                sa[i] = i;
            }
            var f = sa.Slice(boundary);
            f.CopyTo(se);
            sa.Slice(0, boundary).CopyTo(se.Slice(f.Length));
            AdaptiveOptimizedGrailSort.InsertBufferForwardsUnordered(ref MemoryMarshal.GetReference(sa), (nuint)boundary, (nuint)(sa.Length - boundary));
            Assert.Multiple(() =>
            {
                var se2 = exp.AsSpan();
                var sa2 = act.AsSpan();
                var ins = guard + size - boundary;
                Assert.That(sa2.Slice(0, ins).ToArray(), Is.EqualTo(se2.Slice(0, ins).ToArray()));
                Assert.That(sa2.Slice(ins, boundary).ToArray(), Is.EquivalentTo(se2.Slice(ins, boundary).ToArray()));
                Assert.That(sa2.Slice(guard + size).ToArray(), Is.EqualTo(se2.Slice(guard + size).ToArray()));
            });
        }

        [TestCaseSource(nameof(SizeAndBoundaryTestCaseSource))]
        public void InsertBufferBackwardsUnorderedInsertsValuesCorrectly(int size, int boundary)
        {
            var guard = 512;
            var exp = new int[size + guard * 2];
            var act = new int[exp.Length];
            var sep = exp.AsSpan();
            var sap = act.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(sap));
            sap.CopyTo(sep);
            var sa = sap.Slice(guard, size);
            var se = sep.Slice(guard, size);
            for (int i = 0; i < sa.Length; i++)
            {
                sa[i] = i;
            }
            var f = sa.Slice(boundary);
            f.CopyTo(se);
            sa.Slice(0, boundary).CopyTo(se.Slice(f.Length));
            AdaptiveOptimizedGrailSort.InsertBufferBackwardsUnordered(ref MemoryMarshal.GetReference(sa), (nuint)boundary, (nuint)(sa.Length - boundary));
            Assert.Multiple(() =>
            {
                var se2 = exp.AsSpan();
                var sa2 = act.AsSpan();
                Assert.That(sa2.Slice(0, guard).ToArray(), Is.EqualTo(se2.Slice(0, guard).ToArray()));
                Assert.That(sa2.Slice(guard, size - boundary).ToArray(), Is.EquivalentTo(se2.Slice(guard, size - boundary).ToArray()));
                Assert.That(sa2.Slice(guard + size - boundary).ToArray(), Is.EqualTo(se2.Slice(guard + size - boundary).ToArray()));
            });
        }

        [TestCaseSource(nameof(SizeBoundaryBufferExtraSizeTestCaseSource))]
        public void MergeForwardsLargeStructMergesCorrectly(int valueSize, int boundary, int bufferExtraSize)
        {
            var leftSize = boundary;
            var rightSize = valueSize - boundary;
            var bufferSize = int.Max(leftSize, rightSize) + bufferExtraSize;
            var totalSize = valueSize + bufferSize;
            var guard = 64;
            var exp = new ulong[guard * 2 + totalSize];
            var act = new ulong[exp.Length];
            var sep = exp.AsSpan();
            var sap = act.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(sep));
            sep.CopyTo(sap);
            var se = sep.Slice(guard, totalSize);
            var sa = sap.Slice(guard, totalSize);
            se.Slice(0, bufferSize).CopyTo(se.Slice(valueSize));
            var sev = se.Slice(0, valueSize);
            var sav = sa.Slice(bufferSize, valueSize);
            for (int i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                sev[i] = ui >> 1;
            }
            RandomNumberGenerator.Shuffle(sev);
            for (int i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= sev[i] << 32;
                sev[i] = item;
            }
            sev.CopyTo(sav);
            sev.Sort();
            se.Slice(valueSize).CopyTo(sa);
            var avl = sav.Slice(0, boundary);
            var avr = sav.Slice(boundary);
            avl.Sort();
            avr.Sort();
            AdaptiveOptimizedGrailSort.MergeForwardsLargeStruct
                <ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sa), (nuint)bufferSize, (nuint)leftSize, (nuint)rightSize);
            Assert.Multiple(() =>
            {
                var se2 = exp.AsSpan();
                var sa2 = act.AsSpan();
                var ins = guard + valueSize;
                Assert.That(sa2.Slice(0, ins).ToArray(), Is.EqualTo(se2.Slice(0, ins).ToArray()));
                Assert.That(sa2.Slice(ins, bufferSize).ToArray(), Is.EquivalentTo(se2.Slice(ins, bufferSize).ToArray()));
                Assert.That(sa2.Slice(guard + totalSize).ToArray(), Is.EqualTo(se2.Slice(guard + totalSize).ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeTestCaseSource))]
        public void MergeBackwardsLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int valueSize, int boundary, int bufferExtraSize, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var leftSize = boundary;
            var rightSize = valueSize - boundary;
            var bufferSize = int.Max(leftSize, rightSize) + bufferExtraSize;
            var totalSize = valueSize + bufferSize;
            var guard = 64;
            var exp = new ulong[guard * 2 + totalSize];
            var act = new ulong[exp.Length];
            var sep = exp.AsSpan();
            var sap = act.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(sep));
            sep.CopyTo(sap);
            var se = sep.Slice(guard, totalSize);
            var sa = sap.Slice(guard, totalSize);
            se.Slice(valueSize).CopyTo(se);
            var sev = se.Slice(bufferSize, valueSize);
            var sav = sa.Slice(0, valueSize);
            for (int i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                sev[i] = ui >> 1;
            }
            TSequencePermutationProvider.Permute(sev, parameter);
            for (int i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= sev[i] << 32;
                sev[i] = item;
            }
            sev.CopyTo(sav);
            sev.Sort();
            se.Slice(0, bufferSize).CopyTo(sa.Slice(valueSize));
            var avl = sav.Slice(0, boundary);
            var avr = sav.Slice(boundary);
            avl.Sort();
            avr.Sort();
            AdaptiveOptimizedGrailSort.MergeBackwardsLargeStruct
                <ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sa), (nuint)leftSize, (nuint)rightSize, (nuint)bufferSize);
            Assert.Multiple(() =>
            {
                var se2 = exp.AsSpan();
                var sa2 = act.AsSpan();
                var ins = guard;
                Assert.That(sa2.Slice(0, ins).ToArray(), Is.EqualTo(se2.Slice(0, ins).ToArray()));
                Assert.That(sa2.Slice(ins, bufferSize).ToArray(), Is.EquivalentTo(se2.Slice(ins, bufferSize).ToArray()));
                Assert.That(sa2.Slice(guard + bufferSize).ToArray(), Is.EqualTo(se2.Slice(guard + bufferSize).ToArray()));
            });
        }
    }
}
