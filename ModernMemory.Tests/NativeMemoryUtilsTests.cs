using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Tests
{
    [TestFixture]
    public partial class NativeMemoryUtilsTests
    {
        private static IEnumerable<int> LengthValues
        {
            get
            {
                for (var i = 1; i < 6; i++)
                {
                    yield return i * 2 - 1;
                }
                yield return sizeof(ulong) * 4;
                yield return sizeof(ulong) * 4 + 1;
                yield return Vector<byte>.Count * 4;
                yield return Vector<byte>.Count * 4 + 1;
                yield return Vector<byte>.Count * 16;
                yield return Vector<byte>.Count * 16 + 1;
                yield return Vector<byte>.Count * 20 + sizeof(ulong) * 6 - 1;
            }
        }

        private static IEnumerable<int> OffsetValues
        {
            get
            {
                for (var i = 1; i < 6; i++)
                {
                    yield return i * 2 - 1;
                }
            }
        }

        private static IEnumerable<TestCaseData> LengthTestCaseSource => LengthValues.Select(a => new TestCaseData(a));
        private static IEnumerable<TestCaseData> OffsetTestCaseSource => OffsetValues.Select(a => new TestCaseData(a));
        #region NoOverlap
        private static void PrepareByteArraysNoOverlap(int size, out byte[] dst, out byte[] exp, out byte[] src, out Span<byte> sD, out Span<byte> sE, out Span<byte> sS, out Span<byte> sDstActual)
        {
            var guard = Vector<byte>.Count * 20 + sizeof(ulong) * 6 - 1;
            var dstSize = size + 2 * guard;
            dst = new byte[dstSize];
            exp = new byte[dstSize];
            src = new byte[size];
            sD = dst.AsSpan();
            sE = exp.AsSpan();
            sS = src.AsSpan();
            RandomNumberGenerator.Fill(sE);
            sE.CopyTo(sD);
            RandomNumberGenerator.Fill(sS);
            sS.CopyTo(sE.Slice(guard));
            sDstActual = sD.Slice(guard);
        }

        [TestCaseSource(nameof(LengthTestCaseSource))]
        public void CopyFromHeadCopiesBytesCorrectlyNoOverlap(int size)
        {
            PrepareByteArraysNoOverlap(size, out _, out _, out _, out var sD, out var sE, out var sS, out var sDstActual);
            NativeMemoryUtils.CopyFromHead(ref MemoryMarshal.GetReference(sDstActual), ref MemoryMarshal.GetReference(sS), (nuint)sS.Length);
            Assert.That(sD.ToArray(), Is.EqualTo(sE.ToArray()));
        }

        [TestCaseSource(nameof(LengthTestCaseSource))]
        public void CopyFromTailCopiesBytesCorrectlyNoOverlap(int size)
        {
            PrepareByteArraysNoOverlap(size, out _, out _, out _, out var sD, out var sE, out var sS, out var sDstActual);
            NativeMemoryUtils.CopyFromTail(ref MemoryMarshal.GetReference(sDstActual), ref MemoryMarshal.GetReference(sS), (nuint)sS.Length);
            Assert.That(sD.ToArray(), Is.EqualTo(sE.ToArray()));
        }
        #endregion
        private static IEnumerable<TestCaseData> LengthAndOffsetTestCaseSource => LengthValues.Concat([65535, 65536, 65537]).SelectMany(a => OffsetValues.Select(b => new TestCaseData(a, b)));
        private static IEnumerable<TestCaseData> SpecialLengthAndOffsetTestCaseSource => Enumerable.Range(0, 257).SelectMany(s => OffsetValues.Select(b => new TestCaseData(s, b)));
        #region Overlapped
        [TestCaseSource(nameof(LengthAndOffsetTestCaseSource))]
        public void CopyFromHeadCopiesBytesCorrectlyOverlapped(int size, int offset)
        {
            var guard = Vector<byte>.Count * 20 + sizeof(ulong) * 6 - 1;
            var dstSize = size + 2 * guard + offset;
            var dst = new byte[dstSize];
            var exp = new byte[dstSize];
            var sD = dst.AsSpan();
            var sE = exp.AsSpan();
            RandomNumberGenerator.Fill(sE);
            sE.CopyTo(sD);
            sE.Slice(guard + offset, size).CopyTo(sE.Slice(guard));
            var sDstActual = sD.Slice(guard);
            var sSrcActual = sD.Slice(guard + offset, size);
            NativeMemoryUtils.CopyFromHead(ref MemoryMarshal.GetReference(sDstActual), ref MemoryMarshal.GetReference(sSrcActual), (nuint)size);
            Assert.That(sD.ToArray(), Is.EqualTo(sE.ToArray()));
        }

        [TestCaseSource(nameof(LengthAndOffsetTestCaseSource))]
        public void CopyFromTailCopiesBytesCorrectlyOverlapped(int size, int offset)
        {
            var guard = Vector<byte>.Count * 20 + sizeof(ulong) * 6 - 1;
            var dstSize = size + 2 * guard + offset;
            var dst = new byte[dstSize];
            var exp = new byte[dstSize];
            var sD = dst.AsSpan();
            var sE = exp.AsSpan();
            RandomNumberGenerator.Fill(sE);
            sE.CopyTo(sD);
            sE.Slice(guard, size).CopyTo(sE.Slice(guard + offset));
            var sDstActual = sD.Slice(guard + offset);
            var sSrcActual = sD.Slice(guard, size);
            NativeMemoryUtils.CopyFromTail(ref MemoryMarshal.GetReference(sDstActual), ref MemoryMarshal.GetReference(sSrcActual), (nuint)size);
            Assert.That(sD.ToArray(), Is.EqualTo(sE.ToArray()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        [TestCaseSource(nameof(LengthAndOffsetTestCaseSource))]
        public void MoveMemoryCopiesBytesCorrectlyOverlappedPositive(int size, int offset)
        {
            var guard = Vector<byte>.Count * 20 + sizeof(ulong) * 6 - 1;
            var dstSize = size + 2 * guard + offset;
            PrepareMoveTest(dstSize, out var dst, out var exp);
            var sD = dst.AsSpan();
            var sE = exp.AsSpan();
            sE.Slice(guard + offset, size).CopyTo(sE.Slice(guard));
            var sDstActual = sD.Slice(guard);
            var sSrcActual = sD.Slice(guard + offset, size);
            NativeMemoryUtils.MoveMemory(ref MemoryMarshal.GetReference(sDstActual), ref MemoryMarshal.GetReference(sSrcActual), (nuint)size);
            Assert.That(sD.ToArray(), Is.EqualTo(sE.ToArray()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void PrepareMoveTest(int dstSize, out byte[] dst, out byte[] exp)
        {
            dst = new byte[dstSize];
            exp = new byte[dstSize];
            var sD = dst.AsSpan();
            var sE = exp.AsSpan();
            RandomNumberGenerator.Fill(sE);
            sE.CopyTo(sD);
        }

        [TestCaseSource(nameof(LengthAndOffsetTestCaseSource))]
        public void MoveMemoryCopiesBytesCorrectlyOverlappedNegative(int size, int offset)
        {
            var guard = Vector<byte>.Count * 20 + sizeof(ulong) * 6 - 1;
            var dstSize = size + 2 * guard + offset;
            PrepareMoveTest(dstSize, out var dst, out var exp);
            var sD = dst.AsSpan();
            var sE = exp.AsSpan();
            sE.Slice(guard, size).CopyTo(sE.Slice(guard + offset));
            var sDstActual = sD.Slice(guard + offset);
            var sSrcActual = sD.Slice(guard, size);
            NativeMemoryUtils.MoveMemory(ref MemoryMarshal.GetReference(sDstActual), ref MemoryMarshal.GetReference(sSrcActual), (nuint)size);
            Assert.That(sD.ToArray(), Is.EqualTo(sE.ToArray()));
        }
        #endregion

        #region CopySpecialLengths
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        [TestCaseSource(nameof(SpecialLengthAndOffsetTestCaseSource))]
        public void CopySpecialLengthsCopiesBytesCorrectlyOverlappedPositive(int size, int offset)
        {
            var guard = Vector<byte>.Count * 20 + sizeof(ulong) * 6 - 1;
            var dstSize = size + 2 * guard + offset;
            PrepareMoveTest(dstSize, out var dst, out var exp);
            var sD = dst.AsSpan();
            var sE = exp.AsSpan();
            sE.Slice(guard + offset, size).CopyTo(sE.Slice(guard));
            var sDstActual = sD.Slice(guard);
            var sSrcActual = sD.Slice(guard + offset, size);
#pragma warning disable CA1857 // A constant is expected for the parameter
            NativeMemoryUtils.MoveMemoryConstant(ref MemoryMarshal.GetReference(sDstActual), ref MemoryMarshal.GetReference(sSrcActual), (uint)size);
#pragma warning restore CA1857 // A constant is expected for the parameter
            Assert.That(sD.ToArray(), Is.EqualTo(sE.ToArray()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        [TestCaseSource(nameof(SpecialLengthAndOffsetTestCaseSource))]
        public void CopySpecialLengthsCopiesBytesCorrectlyOverlappedNegative(int size, int offset)
        {
            var guard = Vector<byte>.Count * 20 + sizeof(ulong) * 6 - 1;
            var dstSize = size + 2 * guard + offset;
            PrepareMoveTest(dstSize, out var dst, out var exp);
            var sD = dst.AsSpan();
            var sE = exp.AsSpan();
            sE.Slice(guard, size).CopyTo(sE.Slice(guard + offset));
            var sDstActual = sD.Slice(guard + offset);
            var sSrcActual = sD.Slice(guard, size);
#pragma warning disable CA1857 // A constant is expected for the parameter
            NativeMemoryUtils.MoveMemoryConstant(ref MemoryMarshal.GetReference(sDstActual), ref MemoryMarshal.GetReference(sSrcActual), (uint)size);
#pragma warning restore CA1857 // A constant is expected for the parameter
            Assert.That(sD.ToArray(), Is.EqualTo(sE.ToArray()));
        }
        #endregion
    }
}
