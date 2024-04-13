using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Sorting;
using ModernMemory.Utils;

using static System.Reflection.Metadata.BlobBuilder;

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

        [TestCaseSource(nameof(MergeTestCaseSource))]
        public void MergeForwardsLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int valueSize, int boundary, int bufferExtraSize, TParameter parameter)
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
            se.Slice(0, bufferSize).CopyTo(se.Slice(valueSize));
            var sev = se.Slice(0, valueSize);
            var sav = sa.Slice(bufferSize, valueSize);
            for (var i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                sev[i] = ui >> 1;
            }
            TSequencePermutationProvider.Permute(sev, parameter);
            for (var i = 0; i < sev.Length; i++)
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
            for (var i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                sev[i] = ui >> 1;
            }
            TSequencePermutationProvider.Permute(sev, parameter);
            for (var i = 0; i < sev.Length; i++)
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

        [TestCase(512, 64)]
        [TestCase(512, 7)]
        public void CollectKeysCollectsCorrectly(int length, int uniqueValues)
        {
            var len = (nuint)length;
            var idealKeys = AdaptiveOptimizedGrailSort.CalculateBlockSize(len, out var blocks) + blocks;
            var values = new ulong[length];
            var vs = values.AsSpan();
            for (var i = 0; i < vs.Length; i++)
            {
                vs[i] = (ulong)(i % uniqueValues);
            }
            //vs.Sort();
            //vs.Reverse();
            RandomNumberGenerator.Shuffle(vs);
            for (var i = 0; i < vs.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= vs[i] << 32;
                vs[i] = item;
            }
            var act = new ulong[vs.Length];
            vs.CopyTo(act);
            var sa = act.AsSpan();
            var keys = AdaptiveOptimizedGrailSort.CollectKeys<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (sa.AsNativeSpan(), idealKeys);
            Assert.Multiple(() =>
            {
                Assert.That(keys, Is.EqualTo(nuint.Min((nuint)uniqueValues, idealKeys)));
                Assert.That(act, Is.EquivalentTo(values));
                Assert.That(act.AsSpan(0, (int)keys).ToArray(), Is.Unique.And.Ordered);
                Assert.That(act.AsSpan((int)keys).ToArray().Select(a => (uint)a).ToList(), Is.Ordered);
            });
        }

        [TestCase(16, 16, 16, AdaptiveOptimizedGrailSort.Subarray.Left, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(16, 16, 16, AdaptiveOptimizedGrailSort.Subarray.Left, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(32, 16, 32, AdaptiveOptimizedGrailSort.Subarray.Left, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(32, 16, 32, AdaptiveOptimizedGrailSort.Subarray.Left, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(8, 16, 16, AdaptiveOptimizedGrailSort.Subarray.Left, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        public void LocalMergeForwardsLargeStructMergesCorrectly<TSequencePermutationProvider, TParameter>(int bonusSize, int blockSize, int keys, AdaptiveOptimizedGrailSort.Subarray subarray, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var valueSize = bonusSize + blockSize + keys;
            var totalSize = guard * 2 + valueSize;
            var exp = new ulong[guard * 2 + totalSize];
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
            for (var i = 0; i < seq.Length; i++)
            {
                var ui = (uint)i;
                seq[i] = ui >> 1;
            }
            TSequencePermutationProvider.Permute(seq, parameter);
            for (var i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= sev[i] << 32;
                sev[i] = item;
            }
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

        [TestCase(16, 16, 16, AdaptiveOptimizedGrailSort.Subarray.Right, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(16, 16, 16, AdaptiveOptimizedGrailSort.Subarray.Right, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(32, 16, 32, AdaptiveOptimizedGrailSort.Subarray.Right, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(32, 16, 32, AdaptiveOptimizedGrailSort.Subarray.Right, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(8, 16, 16, AdaptiveOptimizedGrailSort.Subarray.Right, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
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
            for (var i = 0; i < sek.Length; i++)
            {
                sek[i] = ~(ulong)i;
            }
            RandomNumberGenerator.Shuffle(sek);
            var seq = sev.Slice(0, mergingSize);
            for (var i = 0; i < seq.Length; i++)
            {
                var ui = (uint)i;
                seq[i] = ui >> 1;
            }
            TSequencePermutationProvider.Permute(seq, parameter);
            for (var i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= sev[i] << 32;
                sev[i] = item;
            }
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sav = sa.Slice(guard, mergingSize);
            var saqL = sav.Slice(blockSize);
            var saqR = sav.Slice(0, blockSize);
            saqL.Sort();
            saqR.Sort();
            seq.Sort();
            var nKeys = (nuint)keys;
            var (_, currentBlockLength) = AdaptiveOptimizedGrailSort.LocalMergeBackwardsLargeStruct<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (ref MemoryMarshal.GetReference(sav), (nuint)blockSize, (nuint)bonusSize, nKeys, subarray);
            var sortedLength = (nuint)sev.Length - currentBlockLength - nKeys;
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

        [TestCase(16, 16, AdaptiveOptimizedGrailSort.Subarray.Left, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(16, 16, AdaptiveOptimizedGrailSort.Subarray.Left, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(32, 16, AdaptiveOptimizedGrailSort.Subarray.Left, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(32, 16, AdaptiveOptimizedGrailSort.Subarray.Left, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(8, 16, AdaptiveOptimizedGrailSort.Subarray.Left, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
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
            for (var i = 0; i < seq.Length; i++)
            {
                var ui = (uint)i;
                seq[i] = ui >> 1;
            }
            TSequencePermutationProvider.Permute(seq, parameter);
            for (var i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= sev[i] << 32;
                sev[i] = item;
            }
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

        [TestCase(5, 64, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(5, 64, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(5, 64, 0, TypeArgs = [typeof(RandomPermutationProvider), typeof(int)])]
        [TestCase(5, 63, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(5, 63, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(5, 63, 0, TypeArgs = [typeof(RandomPermutationProvider), typeof(int)])]
        [TestCase(5, 65, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(5, 65, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(5, 65, 0, TypeArgs = [typeof(RandomPermutationProvider), typeof(int)])]
        [TestCase(5, 1024, null, TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)])]
        [TestCase(5, 1024, 3, TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)])]
        [TestCase(5, 1024, 0, TypeArgs = [typeof(RandomPermutationProvider), typeof(int)])]
        public void BuildBlocksBuildsCorrectly<TSequencePermutationProvider, TParameter>(int bufferSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
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
            for (var i = 0; i < sek.Length; i++)
            {
                sek[i] = ~(ulong)i;
            }
            RandomNumberGenerator.Shuffle(sek);
            var seq = sev.Slice(sek.Length);
            for (var i = 0; i < seq.Length; i++)
            {
                var ui = (uint)i;
                seq[i] = ui >> 1;
            }
            TSequencePermutationProvider.Permute(seq, parameter);
            for (var i = 0; i < sev.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= sev[i] << 32;
                sev[i] = item;
            }
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sav = sa.Slice(guard, mergingSize);
            AdaptiveOptimizedGrailSort.BuildBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>>
                (sav, (nuint)bufferSizeExponent);
            Assert.Multiple(() =>
            {
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
                var seb = sev.Slice(0, (nuint)bufferSize);
                var sab = sav.Slice(0, (nuint)bufferSize);
                Assert.That(sab.ToArray(), Is.EquivalentTo(seb.ToArray()));
                var sar = sav.Slice(sab.Length);
                while (!sar.IsEmpty)
                {
                    var sbb = sar.SliceWhileIfLongerThan((nuint)bufferSize * 2);
                    Assert.That(sbb.ToArray(), Is.Ordered);
                    sar = sar.Slice(sbb.Length);
                }
            });
        }

        private static IEnumerable<(int blockSizeExponent, int size)> GenerateSortBlocksSizeValues()
            => [(3, 63), (3, 64), (3, 65), (3, 72), (4, 1024), (3, 127)];

        private static IEnumerable<TestCaseData> SortBlocksTestCaseSource()
            => GenerateSortBlocksSizeValues().SelectMany<(int blockSizeExponent, int size), TestCaseData>(b =>
            [
                new TestCaseData(b.blockSizeExponent, b.size, null) { TypeArgs = [typeof(IdentityPermutationProvider<object>), typeof(object)] },
                new TestCaseData(b.blockSizeExponent, b.size, null) { TypeArgs = [typeof(ReversePermutationProvider<object>), typeof(object)] },
                new TestCaseData(b.blockSizeExponent, b.size, 3) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, b.size / 2) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, b.size / 3) { TypeArgs = [typeof(RotatedPermutationProvider), typeof(int)] },
                new TestCaseData(b.blockSizeExponent, b.size, 0) { TypeArgs = [typeof(RandomPermutationProvider), typeof(int)] },
            ]);

        [TestCaseSource(nameof(SortBlocksTestCaseSource))]
        public void SortBlocksSortsCorrectlyHead<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var keys = size >> blockSizeExponent;
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            for (var i = 0; i < sek.Length; i++)
            {
                sek[i] = ((ulong)i << 32) | 0xffff_fffful;
            }
            sek.Sort();
            var seq = se.Slice(guard + keys, size);
            for (var i = 0; i < seq.Length; i++)
            {
                var ui = (uint)i;
                seq[i] = ui >> 1;
            }
            TSequencePermutationProvider.Permute(seq, parameter);
            for (var i = 0; i < seq.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= seq[i] << 32;
                seq[i] = item;
            }
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeFalse>
                (ref sak[0], sav, blockSizeExponent);
            sak.CopyTo(sek);
            var ec = seq.ToArray();
            var sec = ec.AsSpan();
            for (var i = 0; i < sak.Length; i++)
            {
                var block = (int)(sak[i] >> 32);
                var bp = block << blockSizeExponent;
                sec.Slice(bp, blockSize).CopyTo(seq.Slice(i << blockSizeExponent));
            }
            Assert.Multiple(() =>
            {
                Assert.That(act, Is.EqualTo(exp));
                var sa = act.AsSpan();
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
        public void SortBlocksSortsCorrectlyTail<TSequencePermutationProvider, TParameter>(int blockSizeExponent, int size, TParameter parameter)
            where TSequencePermutationProvider : ISequencePermutationProvider<TParameter>
        {
            var guard = 8;
            var nSize = (nuint)size;
            var blockSize = 1 << blockSizeExponent;
            var keys = size >> blockSizeExponent;
            var totalSize = guard * 2 + size + keys;
            var exp = new ulong[totalSize];
            var se = exp.AsSpan();
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(se));
            var sek = se.Slice(guard, keys);
            for (var i = 0; i < sek.Length; i++)
            {
                sek[i] = ((ulong)i << 32) | 0xffff_fffful;
            }
            sek.Sort();
            var seq = se.Slice(guard + keys, size);
            for (var i = 0; i < seq.Length; i++)
            {
                var ui = (uint)i;
                seq[i] = ui >> 1;
            }
            TSequencePermutationProvider.Permute(seq, parameter);
            for (var i = 0; i < seq.Length; i++)
            {
                var ui = (uint)i;
                var item = (ulong)ui;
                item |= seq[i] << 32;
                seq[i] = item;
            }
            var subarraySize = unchecked((nuint)nint.MinValue) >> BitOperations.LeadingZeroCount(nSize - 1);
            seq.Slice(0, (int)subarraySize).Sort();
            seq.Slice((int)subarraySize).Sort();
            var act = se.ToArray();
            var sa = act.AsSpan();
            var sak = sa.Slice(guard, keys);
            var sav = sa.Slice(guard + keys, size);
            AdaptiveOptimizedGrailSort.SortBlocks<ulong, TransformedStaticComparisonProxy<ulong, uint, ComparisonOperatorsStaticComparisonProxy<uint>, BitShiftTransform>, TypeTrue>
                (ref sak[0], sav, blockSizeExponent);
            sak.CopyTo(sek);
            var ec = seq.ToArray();
            var sec = ec.AsSpan();
            for (var i = 0; i < sak.Length; i++)
            {
                var block = (int)(sak[i] >> 32);
                var bp = block << blockSizeExponent;
                sec.Slice(bp, blockSize).CopyTo(seq.Slice(i << blockSizeExponent));
            }
            Assert.Multiple(() =>
            {
                Assert.That(act, Is.EqualTo(exp));
                var sa = act.AsSpan();
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
    }
}
