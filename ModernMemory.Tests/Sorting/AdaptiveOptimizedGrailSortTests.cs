using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Sorting;
using ModernMemory.Utils;

using NUnit.Framework.Interfaces;

namespace ModernMemory.Tests.Sorting
{
    [TestFixture]
    public class AdaptiveOptimizedGrailSortTests
    {
        private static IEnumerable<int> BufferExtraSizeValues()
            => [0, 1, 63, 64];

        private static IEnumerable<(int length, int uniqueValues)> SizeAndUniqueValuesParameters()
            => ImmutableArray.Create<(int length, int uniqueValues)>([(512, 64), (512, 7), (512, 0)])
            .Concat(GenerateAllDistinctModulusValues(64).Select<int, (int length, int uniqueValues)>(a => (64, a)));

        private static IEnumerable<TestCaseData> CollectKeysTestCaseSource()
            => SizeAndUniqueValuesParameters().SelectMany<(int length, int uniqueValues), TestCaseData>(b => [
                new TestCaseData(b.length, b.uniqueValues, 0) { TypeArgs = [typeof(IdentityPermutationProvider<int>), typeof(int)] },
                new TestCaseData(b.length, b.uniqueValues, 0) { TypeArgs = [typeof(ReversePermutationProvider<int>), typeof(int)] },
                new TestCaseData(b.length, b.uniqueValues, 0) { TypeArgs = [typeof(RandomPermutationProvider), typeof(int)] },
                ..GeneratePositionParameters(b.length).Except([0]).Select(c => new TestCaseData(b.length, b.uniqueValues, c) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] }),
            ]);

        private static IEnumerable<TestCaseData> CreateMergeBlocksTestCases(IEnumerable<(int blockSizeExponent, int size)> src)
            => src.SelectMany<(int blockSizeExponent, int size), TestCaseData>(b =>
            [
                new TestCaseData(b.blockSizeExponent, b.size, 0) { TypeArgs = [typeof(IdentityPermutationProvider<int>), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, 0) { TypeArgs = [typeof(ReversePermutationProvider<int>), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, 3) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, 1) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, b.size / 2) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, b.size / 3) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, b.size - b.size / 3) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, b.size - 3) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, 0) { TypeArgs = [typeof(RandomPermutationProvider), typeof(int)] },
            ]);

        private static IEnumerable<int> GenerateAllDistinctModulusValues(int max)
            => Enumerable.Range(1, max - 1).DistinctBy(a => max % a).SelectMany<int, int>(a => [a, max - a]).Distinct();
        private static IEnumerable<int> GenerateKeyLengthValues()
            => ImmutableArray.Create([0, 4, 8, 10]).Select(a => 1 << a)
            .SelectMany<int, int>(a => [a, a - 1, a + 1, a * 3]).Where(a => a > 0).Distinct().Order();

        private static void GenerateKeys(Span<ulong> sek)
        {
            for (var i = 0; i < sek.Length; i++)
            {
                sek[i] = ((ulong)i << 32) | uint.MaxValue;
            }
        }

        private static IEnumerable<(int blockSizeExponent, int size)> GenerateMergeBlocksFinalSizeValues()
            => [(3, 63), (3, 65), (3, 66), (3, 71), (3, 72), (3, 73), (4, 1023), (3, 127)];

        private static IEnumerable<(int blockSizeExponent, int size)> GenerateMergeBlocksOrdinalSizeValues()
            => [(3, 64), (4, 1024)];

        private static IEnumerable<int> GeneratePositionParameters(int size)
            => ImmutableArray.Create([size / 2, size / 3, size - size / 3, 3, size - 3]).Where(a => (uint)a < (uint)size).Distinct();

        private static IEnumerable<(int size, int boundary)> GenerateSizeAndBoundaryValues()
                                                    => [(2048, 1), (2048, 2047), (2048, 67), (2048, 2048 - 67), (2048, 768),
                //..GenerateAllDistinctModulusValues(128).Select(size => (128, size)),
                ..GenerateAllDistinctModulusValues(32).Select(a => (32, a)),
                ..GenerateAllDistinctModulusValues(16).Select(a => (16, a)),];

        private static IEnumerable<(int blockSizeExponent, int size)> GenerateSortBlocksSizeValues()
            => [(3, 63), (3, 64), (3, 65), (3, 72), (4, 1024), (3, 127), (0, 16384)];

        private static void GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(TParameter parameter, Span<ulong> values, int uniqueValues = -1) where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            GenerateArray<TSequencePermutationProvider, TParameter>(parameter, values, uniqueValues);
            InsertRelativePosition(values);
        }

        private static void InsertRelativePosition(Span<ulong> values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= values[i] << 32;
                values[i] = item;
            }
        }

        private static void GenerateArray<TSequencePermutationProvider, TParameter>(TParameter parameter, Span<ulong> values, int uniqueValues = -1) where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            GenerateValues(values, uniqueValues);
            TSequencePermutationProvider.Permute(values, parameter);
        }

        private static void GenerateValues(Span<ulong> values, int uniqueValues = -1)
        {
            if (uniqueValues >= 0)
            {
                var j = 0ul;
                var max = (ulong)uniqueValues;
                var i = 0;
                for (; i < values.Length; i++)
                {
                    values[i] = j;
                    var v = ++j >= max;
                    if (v) j = 0;
                }
                values.Sort();
            }
            else
            {
                for (var i = 0; i < values.Length; i++)
                {
                    var ui = (uint)i;
                    values[i] = ui >> 1;
                }
            }
        }

        private static IEnumerable<TestCaseData> MergeBlocksBackwardsTestCaseSource()
                    => CreateMergeBlocksTestCases(GenerateMergeBlocksFinalSizeValues().Concat(GenerateMergeBlocksOrdinalSizeValues()));

        private static IEnumerable<TestCaseData> MergeBlocksFinalTestCaseSource()
            => CreateMergeBlocksTestCases(GenerateMergeBlocksFinalSizeValues());

        private static IEnumerable<TestCaseData> MergeBlocksOrdinalTestCaseSource()
            => CreateMergeBlocksTestCases(GenerateMergeBlocksOrdinalSizeValues());

        private static IEnumerable<TestCaseData> MergeLazyTestCaseSource()
            => GenerateSizeAndBoundaryValues().SelectMany<(int size, int boundary), TestCaseData>(a =>
            [
                new TestCaseData(a.size, a.boundary, 0) { TypeArgs = [typeof(IdentityPermutationProvider<int>), typeof(int)] },
                new TestCaseData(a.size, a.boundary, 0) { TypeArgs = [typeof(ReversePermutationProvider<int>), typeof(int)] },
                new TestCaseData(a.size, a.boundary, a.boundary) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(a.size, a.boundary, 0) { TypeArgs = [typeof(RandomPermutationProvider), typeof(int)] },
            ]);

        private static IEnumerable<TestCaseData> MergeTestCaseSource()
            => GenerateSizeAndBoundaryValues().SelectMany(a => BufferExtraSizeValues().SelectMany<int, TestCaseData>(b =>
            [
                new TestCaseData(a.size, a.boundary, b, 0) { TypeArgs = [typeof(IdentityPermutationProvider<int>), typeof(int)] },
                new TestCaseData(a.size, a.boundary, b, 0) { TypeArgs = [typeof(ReversePermutationProvider<int>), typeof(int)] },
                new TestCaseData(a.size, a.boundary, b, a.boundary) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
            ]));

        private static IEnumerable<TestCaseData> SizeAndBoundaryTestCaseSource()
                                                                    => GenerateSizeAndBoundaryValues().Select(a => new TestCaseData(a.size, a.boundary));

        private static IEnumerable<TestCaseData> SizeBoundaryBufferExtraSizeTestCaseSource()
            => GenerateSizeAndBoundaryValues().SelectMany(a => BufferExtraSizeValues().Select(b => new TestCaseData(a.size, a.boundary, b)));

        private static IEnumerable<TestCaseData> SortBlocksTestCaseSource()
            => GenerateSortBlocksSizeValues().SelectMany<(int blockSizeExponent, int size), TestCaseData>(b =>
            [
                new TestCaseData(b.blockSizeExponent, b.size, 0) { TypeArgs = [typeof(IdentityPermutationProvider<int>), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, 0) { TypeArgs = [typeof(ReversePermutationProvider<int>), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, 3) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, b.size / 2) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, b.size / 3) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, 0) { TypeArgs = [typeof(RandomPermutationProvider), typeof(int)] },
            ]);

        private static IEnumerable<TestCaseData> SortKeysTestCaseSource()
                    => GenerateKeyLengthValues().SelectMany(a => BufferExtraSizeValues().Select(b => a + b)
            .Concat([0, a - 1, 1 << ~BitOperations.LeadingZeroCount((uint)a - 1), a / 8]).Where(b => b >= 0).Distinct()
            .SelectMany(b => GeneratePermutationTestCases(a, a, b)));

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S3220:Method calls should not resolve ambiguously to overloads with \"params\"", Justification = "Intentional")]
        private static IEnumerable<TestCaseData> GeneratePermutationTestCases(int size, params object[] parameters)
            => [
                new TestCaseData([.. parameters, 0]) { TypeArgs = [typeof(IdentityPermutationProvider<int>), typeof(int)] },
                new TestCaseData([.. parameters, 0]) { TypeArgs = [typeof(ReversePermutationProvider<int>), typeof(int)] },
                new TestCaseData([.. parameters, 0]) { TypeArgs = [typeof(RandomPermutationProvider), typeof(int)] },
                ..GeneratePositionParameters(size).Except([0]).Select(c => new TestCaseData([.. parameters, c]) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] }),
            ];

        private static IEnumerable<(int bufferSizeExponent, int size)> BuildBlocksTestCaseValues()
            => [(5, 64), (5, 63), (5, 65), (5, 1024)];

        private static IEnumerable<TestCaseData> BuildBlocksTestCaseSource()
            => BuildBlocksTestCaseValues().SelectMany<(int bufferSizeExponent, int size), TestCaseData>(a =>
            [
                new TestCaseData(a.bufferSizeExponent, a.size, 0) { TypeArgs = [typeof(IdentityPermutationProvider<int>), typeof(int)] },
                new TestCaseData(a.bufferSizeExponent, a.size, 0) { TypeArgs = [typeof(ReversePermutationProvider<int>), typeof(int)] },
                new TestCaseData(a.bufferSizeExponent, a.size, 0) { TypeArgs = [typeof(RandomPermutationProvider), typeof(int)] },
                ..GeneratePositionParameters(a.size).Except([0]).Select(c => new TestCaseData(a.bufferSizeExponent, a.size, c) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] }),
            ]);

        private static IEnumerable<(int size, int subarrayLengthExponent, int blockSizeExponent)> CombineSubarraysTestCaseValues()
            => ImmutableArray.Create([4,3]).SelectMany(sle => Enumerable.Range(1, sle - 1)
            .SelectMany(ble => ImmutableArray.Create([0, 1 << sle, 2 << sle, 3 << sle, 4 << sle, 7 << sle])
            .SelectMany<int, int>(c => [c + 1, c - 1]).Where(c => c > 0).Distinct()
            .Select<int, (int size, int subarrayLengthExponent, int blockSizeExponent)>(size => (size, sle, ble))));

        private static IEnumerable<TestCaseData> CombineSubarraysTestCaseSource()
            => CombineSubarraysTestCaseValues().SelectMany(c => GeneratePermutationTestCases(c.size, c.size, c.subarrayLengthExponent, c.blockSizeExponent));

        private static void AssertGuardRegions<T>(ReadOnlyNativeSpan<T> actual, ReadOnlyNativeSpan<T> expected, nuint guardSize)
        {
            using var aa = new ArrayOwner<T>(guardSize);
            using var ae = new ArrayOwner<T>(guardSize);
            var gaf = actual.Slice(0, guardSize);
            var gef = expected.Slice(0, guardSize);
            gaf.CopyTo(aa.Span);
            gef.CopyTo(ae.Span);
            Assert.That(aa, Is.EqualTo(ae));
            var gab = actual.Slice(actual.Length - guardSize);
            var geb = expected.Slice(expected.Length - guardSize);
            gab.CopyTo(aa.Span);
            geb.CopyTo(ae.Span);
            Assert.That(aa, Is.EqualTo(ae));
        }

        [TestCaseSource(nameof(BuildBlocksTestCaseSource))]
        public void BuildBlocksBuildsCorrectly<TSequencePermutationProvider, TParameter>(int bufferSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
            => BuildBlocksTest<TSequencePermutationProvider, TParameter, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>(bufferSizeExponent, size, parameter);

        [TestCaseSource(nameof(BuildBlocksTestCaseSource))]
        public void BuildBlocksDoesNotThrow<TSequencePermutationProvider, TParameter>(int bufferSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
            => BuildBlocksTest<TSequencePermutationProvider, TParameter, RandomizedComparisonProxy<ulong>>(bufferSizeExponent, size, parameter, true);

        private static void BuildBlocksTest<TSequencePermutationProvider, TParameter, TProxy>(int bufferSizeExponent, int size, TParameter parameter, bool isComparisonRandomized = false)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
            where TProxy : IStaticComparisonProxy<ulong>
        {
            var guard = 8;
            var bufferSize = 1 << bufferSizeExponent;
            var mergingSize = bufferSize + size;
            var totalSize = guard * 2 + size + bufferSize;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sev = se.Slice(guard, mergingSize);
            var sek = sev.Slice(0, bufferSize);
            GenerateKeys(sek);
            RandomNumberGenerator.Shuffle(sek);
            var seq = sev.Slice(sek.Length);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var act = se.ToArray();
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var sav = act.AsSpan(guard, mergingSize);
                    AdaptiveOptimizedGrailSort.BuildBlocks<ulong, TProxy>
                    (sav, bufferSizeExponent);
                });
                var se2 = exp.AsNativeSpan();
                var sa2 = act.AsNativeSpan();
                var segL = se2.Slice(0, (nuint)guard);
                var sagL = sa2.Slice(0, (nuint)guard);
                Assert.That(sagL.ToArray(), Is.EqualTo(segL.ToArray()));
                var segR = se2.Slice(se2.Length - (nuint)guard);
                var sagR = sa2.Slice(se2.Length - (nuint)guard);
                Assert.That(sagR.ToArray(), Is.EqualTo(segR.ToArray()));
                var sav = sa2.Slice((nuint)guard, se2.Length - (nuint)guard * 2);
                var sev = se2.Slice((nuint)guard, se2.Length - (nuint)guard * 2);
                var seBuf = sev.Slice(0, (nuint)bufferSize);
                var saBuf = sav.Slice(0, (nuint)bufferSize);
                Assert.That(saBuf.ToArray(), Is.EquivalentTo(seBuf.ToArray()));
                var sar = sav.Slice(saBuf.Length);
                var ser = sev.Slice(saBuf.Length);
                while (!sar.IsEmpty)
                {
                    var sab = sar.SliceWhileIfLongerThan((nuint)bufferSize * 2);
                    var seb = sar.SliceWhileIfLongerThan((nuint)bufferSize * 2);
                    var aBlock = sab.ToArray();
                    Assert.That(aBlock, Is.EquivalentTo(seb.ToArray()));
                    if (!isComparisonRandomized)
                    {
                        Assert.That(aBlock, Is.Ordered);
                    }
                    sar = sar.Slice(sab.Length);
                    ser = ser.Slice(sab.Length);
                }
            });
        }

        [TestCaseSource(nameof(CollectKeysTestCaseSource))]
        public void CollectKeysCollectsCorrectly<TSequencePermutationProvider, TParameter>(int length, int uniqueValues, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var len = (nuint)length;
            var idealKeys = AdaptiveOptimizedGrailSort.CalculateBlockSize(len, out var blocks) + blocks;
            var values = new ulong[length];
            var vs = values.AsSpan();
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, vs, uniqueValues);
            var act = new ulong[vs.Length];
            vs.CopyTo(act);
            var sa = act.AsSpan();
            var sw = new Stopwatch();
            sw.Start();
            var keys = AdaptiveOptimizedGrailSort.CollectKeys<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (sa.AsNativeSpan(), idealKeys);
            sw.Stop();
            Console.WriteLine($"CollectKeys took {sw.Elapsed}");
            Assert.Multiple(() =>
            {
                Assert.That(keys, Is.EqualTo(nuint.Min((nuint)uniqueValues, idealKeys)));
                Assert.That(act.AsSpan(0, (int)keys).ToArray(), Is.Unique.And.Ordered);
                Assert.That(act.AsSpan((int)keys).ToArray().Select(a => (uint)a).ToList(), Is.Ordered);
                act.AsSpan().Sort();
                values.AsSpan().Sort();
                Assert.That(act, Is.EqualTo(values));
            });
        }

        [TestCaseSource(nameof(CollectKeysTestCaseSource))]
        public void CollectKeysDoesNotThrow<TSequencePermutationProvider, TParameter>(int length, int uniqueValues, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var len = (nuint)length;
            var idealKeys = AdaptiveOptimizedGrailSort.CalculateBlockSize(len, out var blocks) + blocks;
            var values = new ulong[length];
            var vs = values.AsSpan();
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, vs, uniqueValues);
            var act = vs.ToArray();
            Assert.Multiple(() =>
            {
                nuint keys;
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sw = new Stopwatch();
                    sw.Start();
                    keys = AdaptiveOptimizedGrailSort.CollectKeys<ulong, RandomizedComparisonProxy<ulong>>
                        (sa.AsNativeSpan(), idealKeys);
                    sw.Stop();
                    Console.WriteLine($"CollectKeys took {sw.Elapsed}");
                });
                Assert.That(act, Is.EquivalentTo(values));
            });
        }

        [TestCaseSource(nameof(CombineSubarraysTestCaseSource))]
        public void CombineSubarraysForwardsCombinesCorrectly<TSequencePermutationProvider, TParameter>(int size, int subarrayLengthExponent, int blockSizeExponent, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var totalSize = guard * 2 + blockSize + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys + blockSize);
            GenerateKeys(sek);
            var seq = se.Slice(guard + keys + blockSize, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys + blockSize);
            var sav = sa.Slice(guard + keys + blockSize, size);
            var sam = sa.Slice(guard + keys, size + blockSize);
            var savR = sav;
            var subarraySize = 1 << subarrayLengthExponent;
            while (!savR.IsEmpty)
            {
                var s = savR;
                if (s.Length > subarraySize) s = s.Slice(0, subarraySize);
                s.Sort();
                savR = savR.Slice(s.Length);
            }
            var mergeSize = subarraySize * 2;
            var nextMergeSize = mergeSize * 2;
            var nextFullMergeEnd = size & (~nextMergeSize + 1);
            var nextFinalMergeSize = size & (nextMergeSize - 1);
            nuint expectedBufferPos = (nuint)nextFullMergeEnd;
            if (nextFinalMergeSize > mergeSize || nextFullMergeEnd == 0 && nextFinalMergeSize > subarraySize) expectedBufferPos += (nuint)nextFinalMergeSize;
            var bufferPos = AdaptiveOptimizedGrailSort.CombineSubarraysForwards<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sak), sam, subarrayLengthExponent, blockSizeExponent);
            Assert.Multiple(() =>
            {
                Assert.That(bufferPos, Is.EqualTo(expectedBufferPos));
                AssertGuardRegions<ulong>(act.AsNativeSpan(), exp.AsNativeSpan(), (nuint)guard);
                var se = exp.AsSpan();
                var sek = se.Slice(guard, keys);
                var seb = se.Slice(guard + keys, blockSize);
                var seq = se.Slice(guard + keys + blockSize, size);
                var sa = act.AsSpan();
                var sac = sa.Slice(guard, sa.Length - guard * 2);
                var sak = sa.Slice(guard, keys);
                var sab = sa.Slice(guard + keys + (int)bufferPos, blockSize);
                Assert.That(sak.ToArray(), Is.EqualTo(sek.ToArray()));
                Assert.That(sab.ToArray(), Is.EquivalentTo(seb.ToArray()));
                var sav = sa.Slice(guard + keys, (int)bufferPos);
                var sav2 = sac.Slice(keys + (int)bufferPos + blockSize);
                var sevR = seq;
                var mergeSize = 2 << subarrayLengthExponent;
                var aba = ImmutableArray.CreateBuilder<ulong>(sav.Length + sav2.Length);
                aba.AddRange(sav);
                aba.AddRange(sav2);
                while (!sevR.IsEmpty)
                {
                    var sem = sevR;
                    if (sem.Length > mergeSize)
                    {
                        sem = sem.Slice(0, mergeSize);
                    }
                    sem.Sort();
                    sevR = sevR.Slice(sem.Length);
                }
                Assert.That(aba.ToImmutable(), Is.EqualTo(seq.ToImmutableArray()));
            });
        }

        [TestCaseSource(nameof(CombineSubarraysTestCaseSource))]
        public void CombineSubarraysBackwardsCombinesCorrectly<TSequencePermutationProvider, TParameter>(int size, int subarrayLengthExponent, int blockSizeExponent, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var totalSize = guard * 2 + blockSize + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys + blockSize);
            GenerateKeys(sek);
            var seq = se.Slice(guard + keys + blockSize, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys + blockSize);
            var sav = sa.Slice(guard + keys + blockSize, size);
            var sam = sa.Slice(guard + keys, size + blockSize);
            var savR = sav;
            var subarraySize = 1 << subarrayLengthExponent;
            while (!savR.IsEmpty)
            {
                var s = savR;
                if (s.Length > subarraySize) s = s.Slice(0, subarraySize);
                s.Sort();
                savR = savR.Slice(s.Length);
            }
            sam.Rotate(blockSize);
            AdaptiveOptimizedGrailSort.CombineSubarraysBackwards<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sak), sam, subarrayLengthExponent, blockSizeExponent);
            Assert.Multiple(() =>
            {
                AssertGuardRegions<ulong>(act.AsNativeSpan(), exp.AsNativeSpan(), (nuint)guard);
                var se = exp.AsSpan();
                var sek = se.Slice(guard, keys);
                var seb = se.Slice(guard + keys, blockSize);
                var seq = se.Slice(guard + keys + blockSize, size);
                var sa = act.AsSpan();
                var sac = sa.Slice(guard, sa.Length - guard * 2);
                var sak = sa.Slice(guard, keys);
                var sab = sa.Slice(guard + keys, blockSize);
                Assert.That(sak.ToArray(), Is.EqualTo(sek.ToArray()));
                Assert.That(sab.ToArray(), Is.EquivalentTo(seb.ToArray()));
                var sav = sac.Slice(keys + blockSize, size);
                var sevR = seq;
                var mergeSize = 2 << subarrayLengthExponent;
                while (!sevR.IsEmpty)
                {
                    var sem = sevR;
                    if (sem.Length > mergeSize)
                    {
                        sem = sem.Slice(0, mergeSize);
                    }
                    sem.Sort();
                    sevR = sevR.Slice(sem.Length);
                }
                Assert.That(sav.ToArray(), Is.EqualTo(seq.ToArray()));
            });
        }

        [TestCaseSource(nameof(CombineSubarraysTestCaseSource))]
        public void CombineSubarraysLazyCombinesCorrectly<TSequencePermutationProvider, TParameter>(int size, int subarrayLengthExponent, int blockSizeExponent, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            GenerateKeys(sek);
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            var sam = sa.Slice(guard + keys, size);
            var savR = sav;
            var subarraySize = 1 << subarrayLengthExponent;
            while (!savR.IsEmpty)
            {
                var s = savR;
                if (s.Length > subarraySize) s = s.Slice(0, subarraySize);
                s.Sort();
                savR = savR.Slice(s.Length);
            }
            AdaptiveOptimizedGrailSort.CombineSubarraysLazy<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sak), sam, subarrayLengthExponent, blockSizeExponent);
            Assert.Multiple(() =>
            {
                AssertGuardRegions<ulong>(act.AsNativeSpan(), exp.AsNativeSpan(), (nuint)guard);
                var se = exp.AsSpan();
                var sek = se.Slice(guard, keys);
                var seq = se.Slice(guard + keys, size);
                var sa = act.AsSpan();
                var sac = sa.Slice(guard, sa.Length - guard * 2);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EqualTo(sek.ToArray()));
                var sav = sac.Slice(keys, size);
                var sevR = seq;
                var mergeSize = 2 << subarrayLengthExponent;
                while (!sevR.IsEmpty)
                {
                    var sem = sevR;
                    if (sem.Length > mergeSize)
                    {
                        sem = sem.Slice(0, mergeSize);
                    }
                    sem.Sort();
                    sevR = sevR.Slice(sem.Length);
                }
                Assert.That(sav.ToArray(), Is.EqualTo(seq.ToArray()));
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
            for (var i = 0; i < sa.Length; i++)
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
            for (var i = 0; i < sa.Length; i++)
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

        private static IEnumerable<(int bonusSize, int blockSize)> LocalMergeSizeValues()
            => [(16, 16), (32, 16), (8, 16)];

        private static IEnumerable<TestCaseData> LocalMergeTestCaseSource()
            => LocalMergeSizeValues()
            .SelectMany(a => ImmutableArray.Create([AdaptiveOptimizedGrailSort.Subarray.Left, AdaptiveOptimizedGrailSort.Subarray.Right])
            .SelectMany(s => ImmutableArray.Create([0, 16]).Select(b => int.Max(a.bonusSize, a.blockSize) + b)
            .SelectMany(b => GeneratePermutationTestCases(a.blockSize + a.bonusSize, a.bonusSize, a.blockSize, b, s))));

        [TestCaseSource(nameof(LocalMergeTestCaseSource))]
        public void LocalMergeBackwardsLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int bonusSize, int blockSize, int keys, AdaptiveOptimizedGrailSort.Subarray subarray, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var mergingSize = bonusSize + blockSize;
            var valueSize = mergingSize + keys;
            var totalSize = guard * 2 + valueSize;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sev = se.Slice(guard, valueSize);
            var sek = sev.Slice(mergingSize, keys);
            GenerateKeys(sek);
            RandomNumberGenerator.Shuffle(sek);
            var seq = sev.Slice(0, mergingSize);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            if (subarray == AdaptiveOptimizedGrailSort.Subarray.Left) seq.Rotate(bonusSize);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sav = sa.Slice(guard, mergingSize);
            var saqL = sav.Slice(blockSize);
            var saqR = sav.Slice(0, blockSize);
            saqL.Sort();
            saqR.Sort();
            var nKeys = (nuint)keys;
            var (newOrigin, currentBlockLength) = AdaptiveOptimizedGrailSort.LocalMergeBackwardsLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sav), (nuint)blockSize, (nuint)bonusSize, nKeys, subarray);
            Console.WriteLine(newOrigin);
            var sortedLength = (nuint)sev.Length - currentBlockLength - nKeys;
            seq.Sort();
            AdaptiveOptimizedGrailSort.Rotate(ref Unsafe.Add(ref MemoryMarshal.GetReference(sev), currentBlockLength), sortedLength, nKeys);
            Assert.Multiple(() =>
            {
                var se2 = exp.AsNativeSpan();
                var sa2 = act.AsNativeSpan();
                var segL = se2.Slice(0, (nuint)guard);
                var sagL = sa2.Slice(0, (nuint)guard);
                Assert.That(sagL.ToArray(), Is.EqualTo(segL.ToArray()));
                var ses = se2.Slice(segL.Length, currentBlockLength);
                var sas = sa2.Slice(segL.Length, currentBlockLength);
                Assert.That(sas.ToArray(), Is.EqualTo(ses.ToArray()));
                var sek = se2.Slice(segL.Length + currentBlockLength, nKeys);
                var sak = sa2.Slice(segL.Length + currentBlockLength, nKeys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(sek.ToArray()));
                var seb = se2.Slice(segL.Length + currentBlockLength + nKeys, sortedLength);
                var sab = sa2.Slice(segL.Length + currentBlockLength + nKeys, sortedLength);
                Assert.That(sab.ToArray(), Is.EqualTo(seb.ToArray()));
                var segR = se2.Slice(se2.Length - (nuint)guard);
                var sagR = sa2.Slice(se2.Length - (nuint)guard);
                Assert.That(sagR.ToArray(), Is.EqualTo(segR.ToArray()));
            });
        }

        [TestCaseSource(nameof(LocalMergeTestCaseSource))]
        public void LocalMergeBackwardsLargeStructDoesNotThrow<TSequencePermutationProvider, TParameter>(int bonusSize, int blockSize, int keys, AdaptiveOptimizedGrailSort.Subarray subarray, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var mergingSize = bonusSize + blockSize;
            var valueSize = mergingSize + keys;
            var totalSize = guard * 2 + valueSize;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sev = se.Slice(guard, valueSize);
            var sek = sev.Slice(mergingSize, keys);
            GenerateKeys(sek);
            RandomNumberGenerator.Shuffle(sek);
            var seq = sev.Slice(0, mergingSize);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            if (subarray == AdaptiveOptimizedGrailSort.Subarray.Left) seq.Rotate(bonusSize);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sav = sa.Slice(guard, mergingSize);
            var saqL = sav.Slice(blockSize);
            var saqR = sav.Slice(0, blockSize);
            saqL.Sort();
            saqR.Sort();
            var nKeys = (nuint)keys;
            Assert.Multiple(() =>
            {
                var currentBlockLength = nuint.MaxValue;
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sav = sa.Slice(guard, mergingSize);
                    (var newOrigin, currentBlockLength) = AdaptiveOptimizedGrailSort.LocalMergeBackwardsLargeStruct<ulong, RandomizedComparisonProxy<ulong>>
                    (ref MemoryMarshal.GetReference(sav), (nuint)blockSize, (nuint)bonusSize, nKeys, subarray);
                    Console.WriteLine(newOrigin);
                });
                var sev = exp.AsSpan(guard, valueSize);
                var sortedLength = (nuint)sev.Length - currentBlockLength - nKeys;
                var seq = sev.Slice(0, mergingSize);
                seq.Sort();
                AdaptiveOptimizedGrailSort.Rotate(ref Unsafe.Add(ref MemoryMarshal.GetReference(sev), currentBlockLength), sortedLength, nKeys);
                var se2 = exp.AsNativeSpan();
                var sa2 = act.AsNativeSpan();
                var segL = se2.Slice(0, (nuint)guard);
                var sagL = sa2.Slice(0, (nuint)guard);
                Assert.That(sagL.ToArray(), Is.EqualTo(segL.ToArray()));
                var sek = se2.Slice(segL.Length + currentBlockLength, nKeys);
                var sak = sa2.Slice(segL.Length + currentBlockLength, nKeys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(sek.ToArray()));
                var ses = se2.Slice(segL.Length, currentBlockLength);
                var sas = sa2.Slice(segL.Length, currentBlockLength);
                var seb = se2.Slice(segL.Length + currentBlockLength + nKeys, sortedLength);
                var sab = sa2.Slice(segL.Length + currentBlockLength + nKeys, sortedLength);
                Assert.That(ImmutableArray.Create([.. sas, .. sab]), Is.EquivalentTo(ImmutableArray.Create([.. ses, .. seb])));
                var segR = se2.Slice(se2.Length - (nuint)guard);
                var sagR = sa2.Slice(se2.Length - (nuint)guard);
                Assert.That(sagR.ToArray(), Is.EqualTo(segR.ToArray()));
            });
        }

        [TestCaseSource(nameof(LocalMergeTestCaseSource))]
        public void LocalMergeForwardsLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int bonusSize, int blockSize, int keys, AdaptiveOptimizedGrailSort.Subarray subarray, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var valueSize = bonusSize + blockSize + keys;
            var totalSize = guard * 2 + valueSize;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sev = se.Slice(guard, valueSize);
            var sek = sev.Slice(0, keys);
            for (var i = 0; i < sek.Length; i++)
            {
                sek[i] = ~(ulong)i;
            }
            RandomNumberGenerator.Shuffle(sek);
            var seq = sev.Slice(keys);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            if (subarray == AdaptiveOptimizedGrailSort.Subarray.Right) seq.Rotate(blockSize);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sav = sa.Slice(guard, valueSize);
            var saq = sav.Slice(keys);
            var saqL = saq.Slice(0, bonusSize);
            var saqR = saq.Slice(bonusSize);
            saqL.Sort();
            saqR.Sort();
            seq.Sort();
            var nKeys = (nuint)keys;
            var (_, currentBlockLength) = AdaptiveOptimizedGrailSort.LocalMergeForwardsLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sav), nKeys, (nuint)bonusSize, subarray, (nuint)blockSize);
            var sortedLength = (nuint)sev.Length - currentBlockLength - nKeys;
            AdaptiveOptimizedGrailSort.Rotate(ref MemoryMarshal.GetReference(sev), nKeys, sortedLength);
            Assert.Multiple(() =>
            {
                var se2 = exp.AsNativeSpan();
                var sa2 = act.AsNativeSpan();
                var segL = se2.Slice(0, (nuint)guard);
                var sagL = sa2.Slice(0, (nuint)guard);
                Assert.That(sagL.ToArray(), Is.EqualTo(segL.ToArray()));
                var ses = se2.Slice(segL.Length, sortedLength);
                var sas = sa2.Slice(segL.Length, sortedLength);
                Assert.That(sas.ToArray(), Is.EqualTo(ses.ToArray()));
                var sek = se2.Slice(segL.Length + sortedLength, nKeys);
                var sak = sa2.Slice(segL.Length + sortedLength, nKeys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(sek.ToArray()));
                var seb = se2.Slice(segL.Length + sortedLength + nKeys, currentBlockLength);
                var sab = sa2.Slice(segL.Length + sortedLength + nKeys, currentBlockLength);
                Assert.That(sab.ToArray(), Is.EqualTo(seb.ToArray()));
                var segR = se2.Slice(se2.Length - (nuint)guard);
                var sagR = sa2.Slice(se2.Length - (nuint)guard);
                Assert.That(sagR.ToArray(), Is.EqualTo(segR.ToArray()));
            });
        }

        [TestCaseSource(nameof(LocalMergeTestCaseSource))]
        public void LocalMergeForwardsLargeStructDoesNotThrow<TSequencePermutationProvider, TParameter>(int bonusSize, int blockSize, int keys, AdaptiveOptimizedGrailSort.Subarray subarray, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var mergingSize = bonusSize + blockSize;
            var valueSize = bonusSize + blockSize + keys;
            var totalSize = guard * 2 + valueSize;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sev = se.Slice(guard, valueSize);
            var sek = sev.Slice(0, keys);
            for (var i = 0; i < sek.Length; i++)
            {
                sek[i] = ~(ulong)i;
            }
            RandomNumberGenerator.Shuffle(sek);
            var seq = sev.Slice(keys);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            if (subarray == AdaptiveOptimizedGrailSort.Subarray.Right) seq.Rotate(blockSize);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sav = sa.Slice(guard, valueSize);
            var saq = sav.Slice(keys);
            var saqL = saq.Slice(0, bonusSize);
            var saqR = saq.Slice(bonusSize);
            saqL.Sort();
            saqR.Sort();
            seq.Sort();
            var nKeys = (nuint)keys;
            Assert.Multiple(() =>
            {
                var currentBlockLength = nuint.MaxValue;
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sav = sa.Slice(guard, valueSize);
                    (var newOrigin, currentBlockLength) = AdaptiveOptimizedGrailSort.LocalMergeForwardsLargeStruct<ulong, RandomizedComparisonProxy<ulong>>
                    (ref MemoryMarshal.GetReference(sav), nKeys, (nuint)bonusSize, subarray, (nuint)blockSize);
                    Console.WriteLine(newOrigin);
                });
                var sev = exp.AsSpan(guard, valueSize);
                var sortedLength = (nuint)sev.Length - currentBlockLength - nKeys;
                AdaptiveOptimizedGrailSort.Rotate(ref MemoryMarshal.GetReference(sev), nKeys, sortedLength);
                var se2 = exp.AsNativeSpan();
                var sa2 = act.AsNativeSpan();
                var segL = se2.Slice(0, (nuint)guard);
                var sagL = sa2.Slice(0, (nuint)guard);
                Assert.That(sagL.ToArray(), Is.EqualTo(segL.ToArray()));
                var sek = se2.Slice(segL.Length + sortedLength, nKeys);
                var sak = sa2.Slice(segL.Length + sortedLength, nKeys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(sek.ToArray()));
                var ses = se2.Slice(segL.Length, sortedLength);
                var sas = sa2.Slice(segL.Length, sortedLength);
                var seb = se2.Slice(segL.Length + sortedLength + nKeys, currentBlockLength);
                var sab = sa2.Slice(segL.Length + sortedLength + nKeys, currentBlockLength);
                Assert.That(ImmutableArray.Create([.. sas, .. sab]), Is.EquivalentTo(ImmutableArray.Create([.. ses, .. seb])));
                var segR = se2.Slice(se2.Length - (nuint)guard);
                var sagR = sa2.Slice(se2.Length - (nuint)guard);
                Assert.That(sagR.ToArray(), Is.EqualTo(segR.ToArray()));
            });
        }

        private static IEnumerable<TestCaseData> LocalMergeLazyTestCaseSource()
            => LocalMergeSizeValues()
            .SelectMany(a => ImmutableArray.Create([AdaptiveOptimizedGrailSort.Subarray.Left, AdaptiveOptimizedGrailSort.Subarray.Right])
            .SelectMany(b => GeneratePermutationTestCases(a.blockSize + a.bonusSize, a.bonusSize, a.blockSize, b)));

        [TestCaseSource(nameof(LocalMergeLazyTestCaseSource))]
        public void LocalMergeLazyLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int bonusSize, int blockSize, AdaptiveOptimizedGrailSort.Subarray subarray, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var valueSize = bonusSize + blockSize;
            var totalSize = guard * 2 + valueSize;
            var exp = new ulong[guard * 2 + totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sev = se.Slice(guard, valueSize);
            var seq = sev;
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            if (subarray == AdaptiveOptimizedGrailSort.Subarray.Right) seq.Rotate(blockSize);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sav = sa.Slice(guard, valueSize);
            var saq = sav;
            var saqL = saq.Slice(0, bonusSize);
            var saqR = saq.Slice(bonusSize);
            saqL.Sort();
            saqR.Sort();
            seq.Sort();
            var nKeys = (nuint)0;
            var (_, currentBlockLength) = AdaptiveOptimizedGrailSort.LocalMergeLazyLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sav), (nuint)bonusSize, (nuint)blockSize, subarray);
            var sortedLength = (nuint)sev.Length - currentBlockLength;
            Assert.Multiple(() =>
            {
                var se2 = exp.AsNativeSpan();
                var sa2 = act.AsNativeSpan();
                var segL = se2.Slice(0, (nuint)guard);
                var sagL = sa2.Slice(0, (nuint)guard);
                Assert.That(sagL.ToArray(), Is.EqualTo(segL.ToArray()));
                var ses = se2.Slice(segL.Length, sortedLength);
                var sas = sa2.Slice(segL.Length, sortedLength);
                Assert.That(sas.ToArray(), Is.EqualTo(ses.ToArray()));
                var seb = se2.Slice(segL.Length + sortedLength + nKeys, currentBlockLength);
                var sab = sa2.Slice(segL.Length + sortedLength + nKeys, currentBlockLength);
                Assert.That(sab.ToArray(), Is.EqualTo(seb.ToArray()));
                var segR = se2.Slice(se2.Length - (nuint)guard);
                var sagR = sa2.Slice(se2.Length - (nuint)guard);
                Assert.That(sagR.ToArray(), Is.EqualTo(segR.ToArray()));
            });
        }

        [TestCaseSource(nameof(LocalMergeLazyTestCaseSource))]
        public void LocalMergeLazyLargeStructDoesNotThrow<TSequencePermutationProvider, TParameter>(int bonusSize, int blockSize, AdaptiveOptimizedGrailSort.Subarray subarray, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var valueSize = bonusSize + blockSize;
            var totalSize = guard * 2 + valueSize;
            var exp = new ulong[guard * 2 + totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sev = se.Slice(guard, valueSize);
            var seq = sev;
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            if (subarray == AdaptiveOptimizedGrailSort.Subarray.Right) seq.Rotate(blockSize);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sav = sa.Slice(guard, valueSize);
            var saq = sav;
            var saqL = saq.Slice(0, bonusSize);
            var saqR = saq.Slice(bonusSize);
            saqL.Sort();
            saqR.Sort();
            seq.Sort();
            var nKeys = (nuint)0;
            Assert.Multiple(() =>
            {
                var currentBlockLength = nuint.MaxValue;
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sav = sa.Slice(guard, valueSize);
                    (var newOrigin, currentBlockLength) = AdaptiveOptimizedGrailSort.LocalMergeLazyLargeStruct<ulong, RandomizedComparisonProxy<ulong>>
                    (ref MemoryMarshal.GetReference(sav), (nuint)bonusSize, (nuint)blockSize, subarray);
                    Console.WriteLine(newOrigin);
                });
                var sev = exp.AsSpan(guard, valueSize);
                var sortedLength = (nuint)sev.Length - currentBlockLength;
                var se2 = exp.AsNativeSpan();
                var sa2 = act.AsNativeSpan();
                var segL = se2.Slice(0, (nuint)guard);
                var sagL = sa2.Slice(0, (nuint)guard);
                Assert.That(sagL.ToArray(), Is.EqualTo(segL.ToArray()));
                var ses = se2.Slice(segL.Length, sortedLength + currentBlockLength);
                var sas = sa2.Slice(segL.Length, sortedLength + currentBlockLength);
                Assert.That(sas.ToArray(), Is.EquivalentTo(ses.ToArray()));
                var segR = se2.Slice(se2.Length - (nuint)guard);
                var sagR = sa2.Slice(se2.Length - (nuint)guard);
                Assert.That(sagR.ToArray(), Is.EqualTo(segR.ToArray()));
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
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, sev);
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

        [TestCaseSource(nameof(MergeTestCaseSource))]
        public void MergeBackwardsLargeStructDoesNotThrow<TSequencePermutationProvider, TParameter>(int valueSize, int boundary, int bufferExtraSize, TParameter parameter)
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
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, sev);
            sev.CopyTo(sav);
            sev.Sort();
            se.Slice(0, bufferSize).CopyTo(sa.Slice(valueSize));
            var avl = sav.Slice(0, boundary);
            var avr = sav.Slice(boundary);
            avl.Sort();
            avr.Sort();
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan(guard, totalSize);
                    AdaptiveOptimizedGrailSort.MergeBackwardsLargeStruct
                        <ulong, RandomizedComparisonProxy<ulong>>
                        (ref MemoryMarshal.GetReference(sa), (nuint)leftSize, (nuint)rightSize, (nuint)bufferSize);
                });
                var se2 = exp.AsSpan();
                var sa2 = act.AsSpan();
                var ins = guard;
                Assert.That(sa2.Slice(0, ins).ToArray(), Is.EqualTo(se2.Slice(0, ins).ToArray()));
                Assert.That(sa2.Slice(ins, bufferSize).ToArray(), Is.EquivalentTo(se2.Slice(ins, bufferSize).ToArray()));
                Assert.That(sa2.Slice(guard + bufferSize).ToArray(), Is.EquivalentTo(se2.Slice(guard + bufferSize).ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeLazyTestCaseSource))]
        public void MergeBackwardsLazyLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int valueSize, int boundary, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var totalSize = valueSize;
            var guard = 8;
            var exp = new ulong[guard * 2 + totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var seq = se.Slice(guard, valueSize);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            seq.Slice(0, boundary).Sort();
            seq.Slice(boundary).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var saq = sa.Slice(guard, valueSize);
            AdaptiveOptimizedGrailSort.MergeBackwardsLazyLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref saq[0], (nuint)boundary, (nuint)(valueSize - boundary));
            seq.Sort();
            Assert.That(act, Is.EqualTo(exp));
        }

        [TestCaseSource(nameof(MergeLazyTestCaseSource))]
        public void MergeBackwardsLazyLargeStructDoesNotThrow<TSequencePermutationProvider, TParameter>(int valueSize, int boundary, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var totalSize = valueSize;
            var guard = 8;
            var exp = new ulong[guard * 2 + totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var seq = se.Slice(guard, valueSize);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            seq.Slice(0, boundary).Sort();
            seq.Slice(boundary).Sort();
            var act = se.ToArray();
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var saq = act.AsSpan(guard, valueSize);
                    AdaptiveOptimizedGrailSort.MergeBackwardsLazyLargeStruct<ulong, RandomizedComparisonProxy<ulong>>
                        (ref saq[0], (nuint)boundary, (nuint)(valueSize - boundary));
                });
                Assert.That(act, Is.EquivalentTo(exp));
            });
        }

        [TestCaseSource(nameof(MergeBlocksBackwardsTestCaseSource))]
        public void MergeBlocksBackwardsLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + blockSize + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys + blockSize);
            GenerateKeys(sek);
            se.Slice(guard + keys, blockSize + size).Rotate(blockSize);
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            var sam = sa.Slice(guard + keys, size + blockSize);
            var medianKey = sek[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeTrue>
                (ref sak[0], sav, blockSizeExponent);
            AdaptiveOptimizedGrailSort.MergeBlocksBackwardsLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey);
            Assert.Multiple(() =>
            {
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys, size);
                srv.Sort();
                var sav2 = sa.Slice(guard + keys + blockSize, size);
                Assert.That(sav2.ToArray(), Is.EqualTo(srv.ToArray()));
                var srb = se.Slice(guard + keys + size, blockSize);
                var sab2 = sa.Slice(guard + keys, blockSize);
                Assert.That(sab2.ToArray(), Is.EquivalentTo(srb.ToArray()));
                var seg1 = se.Slice(guard + keys + size + blockSize);
                var sag1 = sa.Slice(guard + keys + size + blockSize);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeBlocksBackwardsTestCaseSource))]
        public void MergeBlocksBackwardsLargeStructDoesNotThrow<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + blockSize + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys + blockSize);
            GenerateKeys(sek);
            se.Slice(guard + keys, blockSize + size).Rotate(blockSize);
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            var medianKey = sek[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeTrue>
                (ref sak[0], sav, blockSizeExponent);
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sak = sa.Slice(guard, keys);
                    var sam = sa.Slice(guard + keys, size + blockSize);
                    AdaptiveOptimizedGrailSort.MergeBlocksBackwardsLargeStruct<ulong, RandomizedComparisonProxy<ulong>>
                        (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey);
                });
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys, size);
                var sav2 = sa.Slice(guard + keys + blockSize, size);
                Assert.That(sav2.ToArray(), Is.EquivalentTo(srv.ToArray()));
                var srb = se.Slice(guard + keys + size, blockSize);
                var sab2 = sa.Slice(guard + keys, blockSize);
                Assert.That(sab2.ToArray(), Is.EquivalentTo(srb.ToArray()));
                var seg1 = se.Slice(guard + keys + size + blockSize);
                var sag1 = sa.Slice(guard + keys + size + blockSize);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeBlocksFinalTestCaseSource))]
        public void MergeBlocksForwardsLargeStructMergesCorrectlyFinal<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + blockSize + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            GenerateMergeBlocksForwardsTestData<TSequencePermutationProvider, TParameter>(se, size, guard, nSize, blockSize, keys, parameter);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys + blockSize);
            var sav = sa.Slice(guard + keys + blockSize, size);
            var sam = sa.Slice(guard + keys, size + blockSize);
            var medianKey = sak[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            var lastMerges = AdaptiveOptimizedGrailSort.CountLastMergeBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (sav, blockSizeExponent);
            AdaptiveOptimizedGrailSort.MergeBlocksForwardsLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeTrue>
                (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey, lastMerges);
            Assert.Multiple(() =>
            {
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys + blockSize, size);
                srv.Sort();
                var sav2 = sa.Slice(guard + keys, size);
                Assert.That(sav2.ToArray(), Is.EqualTo(srv.ToArray()));
                var srb = se.Slice(guard + keys, blockSize);
                var sab2 = sa.Slice(guard + keys + size, blockSize);
                Assert.That(sab2.ToArray(), Is.EquivalentTo(srb.ToArray()));
                var seg1 = se.Slice(guard + keys + size + blockSize);
                var sag1 = sa.Slice(guard + keys + size + blockSize);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeBlocksFinalTestCaseSource))]
        public void MergeBlocksForwardsLargeStructDoesNotThrowFinal<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + blockSize + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            GenerateMergeBlocksForwardsTestData<TSequencePermutationProvider, TParameter>(se, size, guard, nSize, blockSize, keys, parameter);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys + blockSize);
            var sav = sa.Slice(guard + keys + blockSize, size);
            var medianKey = sak[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            var lastMerges = AdaptiveOptimizedGrailSort.CountLastMergeBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (sav, blockSizeExponent);

            Assert.Multiple(() =>
            {
                Assert.Multiple(() =>
                {
                    var sa = act.AsSpan();
                    var sak = sa.Slice(guard, keys + blockSize);
                    var sam = sa.Slice(guard + keys, size + blockSize);
                    AdaptiveOptimizedGrailSort.MergeBlocksForwardsLargeStruct<ulong, RandomizedComparisonProxy<ulong>, TypeTrue>
                        (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey, lastMerges);
                });
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys + blockSize, size);
                var sav2 = sa.Slice(guard + keys, size);
                Assert.That(sav2.ToArray(), Is.EquivalentTo(srv.ToArray()));
                var srb = se.Slice(guard + keys, blockSize);
                var sab2 = sa.Slice(guard + keys + size, blockSize);
                Assert.That(sab2.ToArray(), Is.EquivalentTo(srb.ToArray()));
                var seg1 = se.Slice(guard + keys + size + blockSize);
                var sag1 = sa.Slice(guard + keys + size + blockSize);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        private static void GenerateMergeBlocksForwardsTestData<TSequencePermutationProvider, TParameter>(Span<ulong> span, int size, int guard, nuint nSize, int blockSize, int keys, TParameter parameter) where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(span));
            var sek = span.Slice(guard, keys + blockSize);
            GenerateKeys(sek);
            var seq = span.Slice(guard + keys + blockSize, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
        }

        [TestCaseSource(nameof(MergeBlocksOrdinalTestCaseSource))]
        public void MergeBlocksForwardsLargeStructMergesCorrectlyOrdinal<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = size >> blockSizeExponent;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + blockSize + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            GenerateMergeBlocksForwardsTestData<TSequencePermutationProvider, TParameter>(se, size, guard, nSize, blockSize, keys, parameter);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys + blockSize);
            var sav = sa.Slice(guard + keys + blockSize, size);
            var sam = sa.Slice(guard + keys, size + blockSize);
            var medianKey = sak[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            AdaptiveOptimizedGrailSort.MergeBlocksForwardsLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey, 0);
            Assert.Multiple(() =>
            {
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys + blockSize, size);
                srv.Sort();
                var sav2 = sa.Slice(guard + keys, size);
                Assert.That(sav2.ToArray(), Is.EqualTo(srv.ToArray()));
                var srb = se.Slice(guard + keys, blockSize);
                var sab2 = sa.Slice(guard + keys + size, blockSize);
                Assert.That(sab2.ToArray(), Is.EquivalentTo(srb.ToArray()));
                var seg1 = se.Slice(guard + keys + size + blockSize);
                var sag1 = sa.Slice(guard + keys + size + blockSize);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeBlocksOrdinalTestCaseSource))]
        public void MergeBlocksForwardsLargeStructDoesNotThrowOrdinal<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = size >> blockSizeExponent;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + blockSize + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            GenerateMergeBlocksForwardsTestData<TSequencePermutationProvider, TParameter>(se, size, guard, nSize, blockSize, keys, parameter);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys + blockSize);
            var sav = sa.Slice(guard + keys + blockSize, size);
            var medianKey = sak[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sak = sa.Slice(guard, keys + blockSize);
                    var sam = sa.Slice(guard + keys, size + blockSize);
                    AdaptiveOptimizedGrailSort.MergeBlocksForwardsLargeStruct<ulong, RandomizedComparisonProxy<ulong>, TypeFalse>
                        (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey, 0);
                });
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys + blockSize, size);
                var sav2 = sa.Slice(guard + keys, size);
                Assert.That(sav2.ToArray(), Is.EquivalentTo(srv.ToArray()));
                var srb = se.Slice(guard + keys, blockSize);
                var sab2 = sa.Slice(guard + keys + size, blockSize);
                Assert.That(sab2.ToArray(), Is.EquivalentTo(srb.ToArray()));
                var seg1 = se.Slice(guard + keys + size + blockSize);
                var sag1 = sa.Slice(guard + keys + size + blockSize);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeBlocksFinalTestCaseSource))]
        public void MergeBlocksLazyLargeStructMergesCorrectlyFinal<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            GenerateMergeBlocksLazyTestData<TSequencePermutationProvider, TParameter>(se, size, guard, nSize, keys, parameter);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            var sam = sa.Slice(guard + keys, size);
            var medianKey = sak[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            var lastMerges = AdaptiveOptimizedGrailSort.CountLastMergeBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (sav, blockSizeExponent);
            AdaptiveOptimizedGrailSort.MergeBlocksLazyLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeTrue>
                (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey, lastMerges);
            Assert.Multiple(() =>
            {
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys, size);
                srv.Sort();
                var sav2 = sa.Slice(guard + keys, size);
                Assert.That(sav2.ToArray(), Is.EqualTo(srv.ToArray()));
                var seg1 = se.Slice(guard + keys + size);
                var sag1 = sa.Slice(guard + keys + size);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeBlocksFinalTestCaseSource))]
        public void MergeBlocksLazyLargeStructDoesNotThrowFinal<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            GenerateMergeBlocksLazyTestData<TSequencePermutationProvider, TParameter>(se, size, guard, nSize, keys, parameter);
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            var medianKey = sak[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            var lastMerges = AdaptiveOptimizedGrailSort.CountLastMergeBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (sav, blockSizeExponent);
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sak = sa.Slice(guard, keys);
                    var sam = sa.Slice(guard + keys, size);
                    AdaptiveOptimizedGrailSort.MergeBlocksLazyLargeStruct<ulong, RandomizedComparisonProxy<ulong>, TypeTrue>
                        (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey, lastMerges);
                });
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys, size);
                var sav2 = sa.Slice(guard + keys, size);
                Assert.That(sav2.ToArray(), Is.EquivalentTo(srv.ToArray()));
                var seg1 = se.Slice(guard + keys + size);
                var sag1 = sa.Slice(guard + keys + size);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        private static void GenerateMergeBlocksLazyTestData<TSequencePermutationProvider, TParameter>(Span<ulong> se, int size, int guard, nuint nSize, int keys, TParameter parameter) where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            GenerateKeys(sek);
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
        }

        [TestCaseSource(nameof(MergeBlocksOrdinalTestCaseSource))]
        public void MergeBlocksLazyLargeStructMergesCorrectlyOrdinal<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            GenerateKeys(sek);
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            var sam = sa.Slice(guard + keys, size);
            var medianKey = sek[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            AdaptiveOptimizedGrailSort.MergeBlocksLazyLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey, 0);
            Assert.Multiple(() =>
            {
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys, size);
                srv.Sort();
                var sav2 = sa.Slice(guard + keys, size);
                Assert.That(sav2.ToArray(), Is.EqualTo(srv.ToArray()));
                var seg1 = se.Slice(guard + keys + size);
                var sag1 = sa.Slice(guard + keys + size);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeBlocksOrdinalTestCaseSource))]
        public void MergeBlocksLazyLargeStructDoesNotThrowOrdinal<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var keys = (int)blocks;
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            GenerateKeys(sek);
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            var medianKey = sek[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sak = sa.Slice(guard, keys);
                    var sam = sa.Slice(guard + keys, size);
                    AdaptiveOptimizedGrailSort.MergeBlocksLazyLargeStruct<ulong, RandomizedComparisonProxy<ulong>, TypeFalse>
                        (blockSizeExponent, sam, in MemoryMarshal.GetReference(sak), medianKey, 0);
                });
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srv = se.Slice(guard + keys, size);
                var sav2 = sa.Slice(guard + keys, size);
                Assert.That(sav2.ToArray(), Is.EquivalentTo(srv.ToArray()));
                var seg1 = se.Slice(guard + keys + size);
                var sag1 = sa.Slice(guard + keys + size);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeTestCaseSource))]
        public void MergeForwardsLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int valueSize, int boundary, int bufferExtraSize, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var leftSize = boundary;
            var rightSize = valueSize - boundary;
            var bufferSize = int.Max(leftSize, rightSize) + bufferExtraSize;
            var totalSize = valueSize + bufferSize;
            var guard = 8;
            var exp = new ulong[guard * 2 + totalSize];
            var sep = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(sep));
            var se = sep.Slice(guard, totalSize);
            var sev = se.Slice(bufferSize, valueSize);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, sev);
            var act = sep.ToArray();
            var sap = act.AsSpan();
            var sa = sap.Slice(guard, totalSize);
            var sav = sa.Slice(bufferSize, valueSize);
            var avl = sav.Slice(0, boundary);
            var avr = sav.Slice(boundary);
            avl.Sort();
            avr.Sort();
            sev.Sort();
            AdaptiveOptimizedGrailSort.MergeForwardsLargeStruct
                <ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sa), (nuint)bufferSize, (nuint)leftSize, (nuint)rightSize);
            se.Rotate(bufferSize);
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
        public void MergeForwardsLargeStructDoesNotThrow<TSequencePermutationProvider, TParameter>(int valueSize, int boundary, int bufferExtraSize, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var leftSize = boundary;
            var rightSize = valueSize - boundary;
            var bufferSize = int.Max(leftSize, rightSize) + bufferExtraSize;
            var totalSize = valueSize + bufferSize;
            var guard = 8;
            var exp = new ulong[guard * 2 + totalSize];
            var sep = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(sep));
            var se = sep.Slice(guard, totalSize);
            var sev = se.Slice(bufferSize, valueSize);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, sev);
            var act = sep.ToArray();
            var sap = act.AsSpan();
            var sa = sap.Slice(guard, totalSize);
            var sav = sa.Slice(bufferSize, valueSize);
            var avl = sav.Slice(0, boundary);
            var avr = sav.Slice(boundary);
            avl.Sort();
            avr.Sort();
            sev.Sort();
            se.Rotate(bufferSize);
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan(guard, totalSize);
                    AdaptiveOptimizedGrailSort.MergeForwardsLargeStruct<ulong, RandomizedComparisonProxy<ulong>>
                        (ref MemoryMarshal.GetReference(sa), (nuint)bufferSize, (nuint)leftSize, (nuint)rightSize);
                });
                var se2 = exp.AsSpan();
                var sa2 = act.AsSpan();
                var ins = guard + valueSize;
                Assert.That(sa2.Slice(0, guard).ToArray(), Is.EqualTo(se2.Slice(0, guard).ToArray()));
                Assert.That(sa2.Slice(guard, valueSize).ToArray(), Is.EquivalentTo(se2.Slice(guard, valueSize).ToArray()));
                Assert.That(sa2.Slice(ins, bufferSize).ToArray(), Is.EquivalentTo(se2.Slice(ins, bufferSize).ToArray()));
                Assert.That(sa2.Slice(guard + totalSize).ToArray(), Is.EqualTo(se2.Slice(guard + totalSize).ToArray()));
            });
        }

        [TestCaseSource(nameof(MergeLazyTestCaseSource))]
        public void MergeForwardsLazyLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int valueSize, int boundary, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var totalSize = valueSize;
            var guard = 8;
            var exp = new ulong[guard * 2 + totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var seq = se.Slice(guard, valueSize);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            seq.Slice(0, boundary).Sort();
            seq.Slice(boundary).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var saq = sa.Slice(guard, valueSize);
            AdaptiveOptimizedGrailSort.MergeForwardsLazyLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref saq[0], (nuint)boundary, (nuint)(valueSize - boundary));
            seq.Sort();
            Assert.That(act, Is.EqualTo(exp));
        }

        [TestCaseSource(nameof(MergeLazyTestCaseSource))]
        public void MergeForwardsLazyLargeStructDoesNotThrow<TSequencePermutationProvider, TParameter>(int valueSize, int boundary, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var totalSize = valueSize;
            var guard = 8;
            var exp = new ulong[guard * 2 + totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var seq = se.Slice(guard, valueSize);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            seq.Slice(0, boundary).Sort();
            seq.Slice(boundary).Sort();
            var act = se.ToArray();
            Assert.Multiple(() =>
            {
                var sa = act.AsSpan();
                var saq = sa.Slice(guard, valueSize);
                AdaptiveOptimizedGrailSort.MergeForwardsLazyLargeStruct<ulong, RandomizedComparisonProxy<ulong>>
                (ref saq[0], (nuint)boundary, (nuint)(valueSize - boundary));
                Assert.That(act, Is.EquivalentTo(exp));
            });
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
            for (var i = 0; i < sa.Length; i++)
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
            for (var i = 0; i < sa.Length; i++)
            {
                sa[i] = i;
            }
            var f = sa.Slice(boundary);
            f.CopyTo(se);
            sa.Slice(0, boundary).CopyTo(se.Slice(f.Length));
            AdaptiveOptimizedGrailSort.Rotate(ref MemoryMarshal.GetReference(sa), (nuint)boundary, (nuint)(sa.Length - boundary));
            Assert.That(act, Is.EqualTo(exp));
        }

        [TestCaseSource(nameof(SortBlocksTestCaseSource))]
        public void SortBlocksSortsCorrectlyHead<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = nSize >> blockSizeExponent;
            var keys = (int)blocks;
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            GenerateKeys(sek);
            sek.Sort();
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);

            Assert.Multiple(() =>
            {
                var se = exp.AsSpan();
                var sek = se.Slice(guard, keys);
                var seq = se.Slice(guard + keys, size);
                var sa = act.AsSpan();
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(sek.ToArray()));
                sak.CopyTo(sek);
                var ec = seq.ToArray();
                var sec = ec.AsSpan();
                for (var i = 0; i < sak.Length; i++)
                {
                    var block = (int)(sak[i] >> 32);
                    var bp = block << blockSizeExponent;
                    var s = sec.Slice(bp);
                    if (s.Length > blockSize) s = s.Slice(0, blockSize);
                    s.CopyTo(seq.Slice(i << blockSizeExponent));
                }
                Assert.That(act, Is.EqualTo(exp));
                var sav = sa.Slice(guard + keys, size);
                var bva = new ulong[size >> blockSizeExponent];
                var bsa = bva.AsSpan();
                for (var i = 0; i < bsa.Length; i++)
                {
                    bsa[i] = sav[i << blockSizeExponent];
                }
                Assert.That(bva, Is.Ordered);
            });
        }

        [TestCaseSource(nameof(SortBlocksTestCaseSource))]
        public void SortBlocksDoesNotThrowHead<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = nSize >> blockSizeExponent;
            var keys = (int)blocks;
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            GenerateKeys(sek);
            sek.Sort();
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            Assert.Multiple(() =>
            {
                var se = exp.AsSpan();
                var sek = se.Slice(guard, keys);
                var seq = se.Slice(guard + keys, size);
                var sa = act.AsSpan();
                var sak = sa.Slice(guard, keys);
                var sav = sa.Slice(guard + keys, size);
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sak = sa.Slice(guard, keys);
                    var sav = sa.Slice(guard + keys, size);
                    AdaptiveOptimizedGrailSort.SortBlocks<ulong, RandomizedComparisonProxy<ulong>, TypeFalse>(ref sak[0], sav, blockSizeExponent);
                });
                Assert.That(sak.ToArray(), Is.EquivalentTo(sek.ToArray()));
                sak.CopyTo(sek);
                var ec = seq.ToArray();
                var sec = ec.AsSpan();
                var aba = ImmutableArray.CreateBuilder<ulong>(blockSize);
                var abe = ImmutableArray.CreateBuilder<ulong>(blockSize);
                for (var i = 0; i < sak.Length; i++)
                {
                    aba.Clear();
                    abe.Clear();
                    var block = (int)(sak[i] >> 32);
                    var bp = block << blockSizeExponent;
                    var seb = sec.Slice(bp);
                    if (seb.Length > blockSize) seb = seb.Slice(0, blockSize);
                    var sab = sav.Slice(i << blockSizeExponent, seb.Length);
                    aba.AddRange(sab);
                    abe.AddRange(seb);
                    Assert.That(aba, Is.EquivalentTo(abe));
                }
            });
        }

        [TestCaseSource(nameof(SortBlocksTestCaseSource))]
        public void SortBlocksSortsCorrectlyTail<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = nSize >> blockSizeExponent;
            var keys = (int)blocks;
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            GenerateKeys(sek);
            sek.Sort();
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeTrue>
                (ref sak[0], sav, blockSizeExponent);
            Assert.Multiple(() =>
            {
                var se = exp.AsSpan();
                var sek = se.Slice(guard, keys);
                var seq = se.Slice(guard + keys, size);
                var sa = act.AsSpan();
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(sek.ToArray()));
                sak.CopyTo(sek);
                var ec = seq.ToArray();
                var sec = ec.AsSpan();
                for (var i = 0; i < sak.Length; i++)
                {
                    var block = (int)(sak[i] >> 32);
                    var bp = block << blockSizeExponent;
                    var s = sec.Slice(bp);
                    if (s.Length > blockSize) s = s.Slice(0, blockSize);
                    s.CopyTo(seq.Slice(i << blockSizeExponent));
                }
                Assert.That(act, Is.EqualTo(exp));
                var sav = sa.Slice(guard + keys, size);
                var bva = new ulong[size >> blockSizeExponent];
                var bsa = bva.AsSpan();
                var offset = (1 << blockSizeExponent) - 1;
                for (var i = 0; i < bsa.Length; i++)
                {
                    bsa[i] = sav[(i << blockSizeExponent) + offset];
                }
                Assert.That(bva, Is.Ordered);
            });
        }

        [TestCaseSource(nameof(SortBlocksTestCaseSource))]
        public void SortBlocksDoesNotThrowTail<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = nSize >> blockSizeExponent;
            var keys = (int)blocks;
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            GenerateKeys(sek);
            sek.Sort();
            var seq = se.Slice(guard + keys, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            Assert.Multiple(() =>
            {
                var se = exp.AsSpan();
                var sek = se.Slice(guard, keys);
                var seq = se.Slice(guard + keys, size);
                var sa = act.AsSpan();
                var sak = sa.Slice(guard, keys);
                var sav = sa.Slice(guard + keys, size);
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sak = sa.Slice(guard, keys);
                    var sav = sa.Slice(guard + keys, size);
                    AdaptiveOptimizedGrailSort.SortBlocks<ulong, RandomizedComparisonProxy<ulong>, TypeTrue>(ref sak[0], sav, blockSizeExponent);
                });
                Assert.That(sak.ToArray(), Is.EquivalentTo(sek.ToArray()));
                sak.CopyTo(sek);
                var ec = seq.ToArray();
                var sec = ec.AsSpan();
                var aba = ImmutableArray.CreateBuilder<ulong>(blockSize);
                var abe = ImmutableArray.CreateBuilder<ulong>(blockSize);
                for (var i = 0; i < sak.Length; i++)
                {
                    aba.Clear();
                    abe.Clear();
                    var block = (int)(sak[i] >> 32);
                    var bp = block << blockSizeExponent;
                    var seb = sec.Slice(bp);
                    if (seb.Length > blockSize) seb = seb.Slice(0, blockSize);
                    var sab = sav.Slice(i << blockSizeExponent, seb.Length);
                    aba.AddRange(sab);
                    abe.AddRange(seb);
                    Assert.That(aba, Is.EquivalentTo(abe));
                }
            });
        }

        [TestCaseSource(nameof(SortKeysTestCaseSource))]
        public void SortKeysDoesNotThrowMalfunctionalComparisonProxy<TSequencePermutationProvider, TParameter>(int keys, int bufferSize, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var size = keys;
            var bufferLength = bufferSize;
            var blockSizeExponent = 0;
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + bufferLength + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys + bufferLength);
            GenerateKeys(sek);
            var seq = se.Slice(guard + keys + bufferLength, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys + bufferLength, size);
            var medianKey = sek[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            Assert.Multiple(() =>
            {
                Assert.DoesNotThrow(() =>
                {
                    var sa = act.AsSpan();
                    var sak = sa.Slice(guard, keys);
                    var sab = sa.Slice(guard + keys, bufferLength);
                    AdaptiveOptimizedGrailSort.SortKeys<ulong, PositiveBiasedRandomizedComparisonProxy<ulong>>
                    (sak, sab, medianKey);
                });
                var se = exp.AsSpan();
                var sa = act.AsSpan();
                var seg0 = se.Slice(0, guard);
                var sag0 = sa.Slice(0, guard);
                Assert.That(sag0.ToArray(), Is.EqualTo(seg0.ToArray()));
                var srk = se.Slice(guard, keys);
                var sak = sa.Slice(guard, keys);
                Assert.That(sak.ToArray(), Is.EquivalentTo(srk.ToArray()));
                var srb = se.Slice(guard + keys, bufferLength);
                var sab2 = sa.Slice(guard + keys, bufferLength);
                Assert.That(sab2.ToArray(), Is.EquivalentTo(srb.ToArray()));
                var srv = se.Slice(guard + keys + bufferLength, size);
                srv.Sort();
                var sav2 = sa.Slice(guard + keys + bufferLength, size);
                Assert.That(sav2.ToArray(), Is.EqualTo(srv.ToArray()));
                var seg1 = se.Slice(guard + keys + size + bufferLength);
                var sag1 = sa.Slice(guard + keys + size + bufferLength);
                Assert.That(sag1.ToArray(), Is.EqualTo(seg1.ToArray()));
            });
        }

        [TestCaseSource(nameof(SortKeysTestCaseSource))]
        public void SortKeysSortsCorrectly<TSequencePermutationProvider, TParameter>(int keys, int bufferSize, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var size = keys;
            var bufferLength = bufferSize;
            var blockSizeExponent = 0;
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var blocks = AdaptiveOptimizedGrailSort.GetBlockCount(nSize, blockSizeExponent);
            var nKeys = (nuint)keys;
            var medianKeyPos = (nuint)nint.MinValue >> BitOperations.LeadingZeroCount(blocks - 1);
            var totalSize = guard * 2 + bufferLength + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys + bufferLength);
            GenerateKeys(sek);
            var seq = se.Slice(guard + keys + bufferLength, size);
            GenerateStabilityCheckArray<TSequencePermutationProvider, TParameter>(parameter, seq);
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys + bufferLength, size);
            var sab = sa.Slice(guard + keys, bufferLength);
            var medianKey = sek[(int)medianKeyPos];
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            AdaptiveOptimizedGrailSort.SortKeys<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (sak, sab, medianKey);
            var srk = sek.Slice(0, keys);
            srk.Sort();
            sek.Slice(keys).Sort();
            seq.Sort();
            sab.Sort();
            sav.Sort();
            Assert.That(act, Is.EqualTo(exp));
        }
    }
}
