using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Randomness;
using ModernMemory.Randomness.Permutation;

namespace ModernMemory.Tests.Randomness
{
    [TestFixture]
    public class BufferedRandomNumberReaderTests
    {
        [Test]
        public void Shuffle7ShufflesCorrectly()
        {
            const int Size = 7;
            var f = checked((int)MathUtils.Factorials[Size]);
            byte[] src = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            var m = ParallelEnumerable.Range(0, f).AsUnordered().Select(a =>
            {
                var y = new FixedArray16<byte>();
                Span<byte> s = y;
                var ssp = src.AsSpan();
                ssp.CopyAtMostTo(s);
                PermutationUtils.Shuffle7(s.Slice(0, Size), (uint)a);
                return (y, a);
            });
            Assert.Multiple(() =>
            {
                Assert.That(m.Where(a =>
                {
                    var copy = a.y;
                    Span<byte> q = copy;
                    var y = new FixedArray16<byte>();
                    Span<byte> s = y;
                    var ssp = src.AsSpan();
                    ssp.CopyAtMostTo(s);
                    PermutationUtils.ShuffleAnySmall(s.Slice(0, Size), (uint)a.a);
                    return !s.SequenceEqual(q);
                }).WithMergeOptions(ParallelMergeOptions.NotBuffered), Is.Empty);
                Assert.That(m.Select(q => q.y).Distinct(new FixedArray16Comparer(7)).Count(), Is.EqualTo(f));
            });
        }

        private sealed class FixedArray12Comparer : IEqualityComparer<FixedArray12<byte>>
        {
            public bool Equals(FixedArray12<byte> x, FixedArray12<byte> y)
            {
                ReadOnlySpan<byte> l = x;
                ReadOnlySpan<byte> r = y;
                return l.SequenceEqual(r);
            }
            public int GetHashCode([DisallowNull] FixedArray12<byte> obj)
            {
                ReadOnlySpan<byte> q = obj;
                var m = new HashCode();
                m.AddBytes(q);
                return m.ToHashCode();
            }
        }

        private sealed class FixedArray16Comparer(int size = 16) : IEqualityComparer<FixedArray16<byte>>
        {
            private readonly int size = (int)uint.Min(16, (uint)size);

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public bool Equals(FixedArray16<byte> x, FixedArray16<byte> y)
            {
                var v2_8b = Vector128.LessThan(Vector128.Create(byte.MinValue, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15), Vector128.Create((byte)size));
                ReadOnlySpan<byte> l = x;
                ReadOnlySpan<byte> r = y;
                var v0_8b = Vector128.Create(l) & v2_8b;
                var v1_8b = Vector128.Create(r) & v2_8b;
                return v0_8b == v1_8b;
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public int GetHashCode([DisallowNull] FixedArray16<byte> obj)
            {
                ReadOnlySpan<byte> q = obj;
                var m = new HashCode();
                m.AddBytes(q.Slice(0, size));
                return m.ToHashCode();
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10, Explicit = true)]
        [TestCase(11, Explicit = true)]
        [TestCase(12, Explicit = true)]
        [NonParallelizable]
        public void ShuffleAnySmallShufflesCorrectly(int size)
        {
            var f = checked((int)MathUtils.Factorials[size]);
            byte[] src = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            var m = ParallelEnumerable.Range(0, f).AsUnordered().Select(a =>
            {
                var y = new FixedArray12<byte>();
                Span<byte> s = y;
                var ssp = src.AsSpan();
                ssp.CopyAtMostTo(s);
                PermutationUtils.ShuffleAnySmall(s.Slice(0, size), (uint)a);
                return y;
            });
            Assert.Multiple(() =>
            {
                var sv = SearchValues.Create(src);
                Assert.That(m.Where(a =>
                {
                    var copy = a;
                    Span<byte> y = copy;
                    var broken = y[..size].ContainsAnyExcept(sv);
                    if (!broken) y[..size].Sort();
                    return broken || FindDuplicate(y);
                }).WithMergeOptions(ParallelMergeOptions.NotBuffered), Is.Empty);
                Assert.That(m.Distinct(new FixedArray12Comparer()).Count(), Is.EqualTo(f));
            });
        }

        [Test]
        [Explicit]
        [NonParallelizable]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void ShuffleAnySmallUInt64Shuffles13ElementsCorrectly()
        {
            const int Size = 13;
            byte[] src = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            var m = Enumerable.Range(0, 13).Select(l => ParallelEnumerable.Range(0, 479001600).Where(a =>
            {
                var y = new FixedArray16<byte>();
                Span<byte> s = y;
                var ssp = src.AsSpan();
                ssp.CopyTo(s);
                PermutationUtils.ShuffleAnySmallUInt64(s.Slice(0, Size), (ulong)a * 13ul + (ulong)l);
                var broken = y[..Size].ContainsAnyExceptInRange((byte)0, (byte)(Size - 1));
                if (!broken) y[..Size].Sort();
                return broken || FindDuplicate(y);
            }).AsUnordered());
            Assert.That(m.Any(b => b.Any()), Is.False);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static bool FindDuplicate(ReadOnlySpan<byte> y)
        {
            Vector128<byte> v0;
            if (y.Length < 16)
            {
                Span<byte> s = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
                y.CopyTo(s);
                ReadOnlySpan<byte> zs = s;
                v0 = Vector128.Create(zs);
            }
            else
            {
                v0 = Vector128.Create(y);
            }
            var v1 = Vector128.Shuffle(v0, Vector128.Create(15, byte.MinValue, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14));
            var dup = Vector128.EqualsAny(v0, v1);
            return dup;
        }
    }
}
