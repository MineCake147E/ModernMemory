using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
            var m = Enumerable.Range(0, 5040).Select(a =>
            {
                Span<byte> s = [0xff, 0, 1, 2, 3, 4, 5, 6, 0xff];
                PermutationUtils.Shuffle7(s.Slice(1, 7), (uint)a);
                Assert.That(s[0], Is.EqualTo(0xff));
                return BinaryPrimitives.ReadUInt64LittleEndian(s.Slice(1));
            }).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(m, Is.Unique);
                var sv = SearchValues.Create([0, 1, 2, 3, 4, 5, 6]);
                Assert.That(m.Where(a =>
                {
                    var y = MemoryMarshal.AsBytes(new Span<ulong>(ref a));
                    return y.Slice(0, 7).ContainsAnyExcept(sv) || y[^1] != 0xff;
                }), Is.Empty);
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

        private sealed class FixedArray16Comparer : IEqualityComparer<FixedArray16<byte>>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public bool Equals(FixedArray16<byte> x, FixedArray16<byte> y)
            {
                ReadOnlySpan<byte> l = x;
                ReadOnlySpan<byte> r = y;
                return Vector128.Create(l) == Vector128.Create(r);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            public int GetHashCode([DisallowNull] FixedArray16<byte> obj)
            {
                ReadOnlySpan<byte> q = obj;
                var m = new HashCode();
                m.AddBytes(q);
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
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        public void ShuffleAnySmallShufflesCorrectly(int size)
        {
            var f = checked((int)MathUtils.Factorials[size]);
            byte[] src = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
            src.AsSpan(size).Fill(0xff);
            var m = ParallelEnumerable.Range(0, f).AsUnordered().Select(a =>
            {
                var y = new FixedArray12<byte>();
                Span<byte> s = y;
                var ssp = src.AsSpan();
                ssp.CopyTo(s);
                PermutationUtils.ShuffleAnySmall(s.Slice(0, size), (uint)a);
                return y;
            });
            Assert.Multiple(() =>
            {
                Assert.That(m.Where(a =>
                {
                    var copy = a;
                    Span<byte> y = copy;
                    var broken = y[..size].ContainsAnyExceptInRange((byte)0, (byte)(size - 1));
                    y[..size].Sort();
                    bool dup = false;
                    var last = y[0];
                    var y2 = y[1..size];
                    for (int i = 0; i < y2.Length; i++)
                    {
                        if (last == y2[i])
                        {
                            dup = true;
                            break;
                        }
                        last = y2[i];
                    }
                    return dup || broken;
                }).WithMergeOptions(ParallelMergeOptions.NotBuffered), Is.Empty);
                Assert.That(m.Distinct(new FixedArray12Comparer()).Count(), Is.EqualTo(f));
            });
        }

        [Test]
        [Category("Heavy")]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void ShuffleAnySmallUInt64ShufflesCorrectly()
        {
            const int Size = 13;
            byte[] src = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
            //src.AsSpan(Size).Fill(0xff);
            var m = Enumerable.Range(0, 13).Select(l => ParallelEnumerable.Range(0, 479001600).Select(a =>
            {
                var y = new FixedArray16<byte>();
                Span<byte> s = y;
                var ssp = src.AsSpan();
                ssp.CopyTo(s);
                PermutationUtils.ShuffleAnySmallUInt64(s.Slice(0, Size), (ulong)a * 13ul + (ulong)l);
                return y;
            }).AsUnordered());
            Assert.That(m.Any(b => b.Any(a =>
            {
                var copy = a;
                Span<byte> y = copy;
                var broken = y[..Size].ContainsAnyExceptInRange((byte)0, (byte)(Size - 1));
                if (!broken) y[..Size].Sort();
                return broken || FindDuplicate(y);
            })), Is.False);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static bool FindDuplicate(ReadOnlySpan<byte> y)
        {
            var v0 = Vector128.Create(y);
            var v1 = Vector128.Shuffle(v0, Vector128.Create(15, byte.MinValue, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14));
            var dup = Vector128.EqualsAny(v0, v1);
            return dup;
        }
    }
}
