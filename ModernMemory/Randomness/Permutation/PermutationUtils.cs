using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Randomness.Permutation
{
    public static class PermutationUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ShuffleAnySmallUInt64<T>(scoped Span<T> d, ulong f) => ShuffleAnySmallUInt64(d.AsNativeSpan(), f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ShuffleAnySmallUInt64<T>(scoped NativeSpan<T> d, ulong f)
        {
            Debug.Assert(d.Length < (nuint)MathUtils.Factorials.Length);
#pragma warning disable S907 // "goto" statement should not be used
            switch (d.Length)
            {
                case 20:
                    f = ShuffleStep(d, f, 19);
                    goto case 19;
                case 19:
                    f = ShuffleStep(d, f, 18);
                    goto case 18;
                case 18:
                    f = ShuffleStep(d, f, 17);
                    goto case 17;
                case 17:
                    f = ShuffleStep(d, f, 16);
                    goto case 16;
                case 16:
                    f = ShuffleStep(d, f, 15);
                    goto case 15;
                case 15:
                    f = ShuffleStep(d, f, 14);
                    goto case 14;
                case 14:
                    f = ShuffleStep(d, f, 13);
                    goto case 13;
                case 13:
                    f = ShuffleStep(d, f, 12);
                    break;
                default:
                    break;
            }
            uint f2 = (uint)f;
            switch (d.Length)
            {
                case 12:
                    f2 = ShuffleStep(d, f2, 11);
                    goto case 11;
                case 11:
                    f2 = ShuffleStep(d, f2, 10);
                    goto case 10;
                case 10:
                    f2 = ShuffleStep(d, f2, 9);
                    goto case 9;
                case 9:
                    f2 = ShuffleStep(d, f2, 8);
                    goto case 8;
                case 8:
                    f2 = ShuffleStep(d, f2, 7);
                    goto case 7;
                case 7:
                    f2 = ShuffleStep(d, f2, 6);
                    goto case 6;
                case 6:
                    f2 = ShuffleStep(d, f2, 5);
                    goto case 5;
                case 5:
                    f2 = ShuffleStep(d, f2, 4);
                    goto case 4;
                case 4:
                    f2 = ShuffleStep(d, f2, 3);
                    goto case 3;
                case 3:
                    f2 = ShuffleStep(d, f2, 2);
                    goto case 2;
                case 2:
                    ShuffleStep(d, f2, 1);
                    break;
                default:
                    break;
            }
#pragma warning restore S907 // "goto" statement should not be used
        }

        internal static void ShuffleAnySmallUInt64Old<T>(scoped NativeSpan<T> d, ulong f)
        {
            var i = d.Length - 1;
            Debug.Assert(d.Length < (nuint)MathUtils.Factorials.Length);
            for (; f >> 32 > 0 && i > 0; i--)
            {
                var q = (uint)(i + 1);
                (f, var v) = Math.DivRem(f, q);
                Debug.Assert(v < d.Length);
                var m = (nuint)v;
                ref var l = ref d.ElementAtUnchecked(m);
                ref var k = ref d.ElementAtUnchecked(i);
                (k, l) = (l, k);
            }
            var f2 = (uint)f;
            for (; i > 0; i--)
            {
                var q = (uint)(i + 1);
                (f2, var v) = Math.DivRem(f2, q);
                Debug.Assert(v < (uint)d.Length);
                var m = v;
                ref var l = ref d.ElementAtUnchecked(m);
                ref var k = ref d.ElementAtUnchecked(i);
                (k, l) = (l, k);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ShuffleAnySmall<T>(scoped Span<T> d, uint f2) => ShuffleAnySmall(d.AsNativeSpan(), f2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void ShuffleAnySmall<T>(scoped NativeSpan<T> d, uint f2)
        {
            Debug.Assert(d.Length < 13);
#pragma warning disable S907 // "goto" statement should not be used
            switch (d.Length)
            {
                case 12:
                    f2 = ShuffleStep(d, f2, 11);
                    goto case 11;
                case 11:
                    f2 = ShuffleStep(d, f2, 10);
                    goto case 10;
                case 10:
                    f2 = ShuffleStep(d, f2, 9);
                    goto case 9;
                case 9:
                    f2 = ShuffleStep(d, f2, 8);
                    goto case 8;
                case 8:
                    f2 = ShuffleStep(d, f2, 7);
                    goto case 7;
                case 7:
                    f2 = ShuffleStep(d, f2, 6);
                    goto case 6;
                case 6:
                    f2 = ShuffleStep(d, f2, 5);
                    goto case 5;
                case 5:
                    f2 = ShuffleStep(d, f2, 4);
                    goto case 4;
                case 4:
                    f2 = ShuffleStep(d, f2, 3);
                    goto case 3;
                case 3:
                    f2 = ShuffleStep(d, f2, 2);
                    goto case 2;
                case 2:
                    ShuffleStep(d, f2, 1);
                    break;
                default:
                    break;
            }
#pragma warning restore S907 // "goto" statement should not be used
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong ShuffleStep<T>(scoped NativeSpan<T> d, ulong f, [ConstantExpected(Max = 20)] uint i)
        {
            (var res, var v) = Math.DivRem(f, i + 1);
            Debug.Assert(v < d.Length);
            Debug.Assert(i < d.Length);
            ref var l = ref d.ElementAtUnchecked((nuint)v);
            ref var k = ref d.ElementAtUnchecked(i);
            (k, l) = (l, k);
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint ShuffleStep<T>(scoped NativeSpan<T> d, uint f, [ConstantExpected(Max = 11)] uint i)
        {
            (var res, var v) = Math.DivRem(f, i + 1);
            Debug.Assert(v < d.Length);
            Debug.Assert(i < d.Length);
            ref var l = ref d.ElementAtUnchecked(v);
            ref var k = ref d.ElementAtUnchecked(i);
            (k, l) = (l, k);
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Shuffle2<T>(scoped Span<T> d, bool swap) => Shuffle2(d.AsNativeSpan(), swap);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Shuffle2<T>(scoped NativeSpan<T> d, bool swap)
        {
            if (swap)
            {
                ref var dr = ref NativeMemoryUtils.GetReference(d);
                (dr, Unsafe.Add(ref dr, 1)) = (Unsafe.Add(ref dr, 1), dr);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Shuffle7<T>(scoped Span<T> d, uint t) => Shuffle7(d.AsNativeSpan(), t);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static void Shuffle7<T>(scoped NativeSpan<T> d, uint t)
        {
            Debug.Assert(d.Length >= 7);
            Debug.Assert(t < 5040);
            var y0 = t;
            var k = (t * 9363) >> 16;
            var y1 = k;
            var u = (k * 10923) >> 16;
            var y2 = u;
            y0 -= k * 7;
            k = (u * 205) >> 10;
            var y3 = k & 0x3;
            t = k >> 2;
            y1 -= u * 6;
            t += t;
            y2 -= k * 5;
            t = 0x924u >> (int)t;
            k = k >= 12 ? 1u : 0;
            var y4 = t & 3;
            var y5 = k & 1;
            ref var dr = ref NativeMemoryUtils.GetReference(d);
            (Unsafe.Add(ref dr, y0), Unsafe.Add(ref dr, 6)) = (Unsafe.Add(ref dr, 6), Unsafe.Add(ref dr, y0));
            (Unsafe.Add(ref dr, y1), Unsafe.Add(ref dr, 5)) = (Unsafe.Add(ref dr, 5), Unsafe.Add(ref dr, y1));
            (Unsafe.Add(ref dr, y2), Unsafe.Add(ref dr, 4)) = (Unsafe.Add(ref dr, 4), Unsafe.Add(ref dr, y2));
            (Unsafe.Add(ref dr, y3), Unsafe.Add(ref dr, 3)) = (Unsafe.Add(ref dr, 3), Unsafe.Add(ref dr, y3));
            (Unsafe.Add(ref dr, y4), Unsafe.Add(ref dr, 2)) = (Unsafe.Add(ref dr, 2), Unsafe.Add(ref dr, y4));
            (Unsafe.Add(ref dr, y5), Unsafe.Add(ref dr, 1)) = (Unsafe.Add(ref dr, 1), Unsafe.Add(ref dr, y5));
        }
    }
}
