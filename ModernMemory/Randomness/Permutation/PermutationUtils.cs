using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Randomness.Permutation
{
    public static class PermutationUtils
    {
        internal static void ShuffleAnySmallUInt64<T>(Span<T> d, ulong f)
        {
            var i = d.Length - 1;
            for (; f >> 32 > 0 && i > 0; i--)
            {
                var q = (uint)(i + 1);
                (f, var v) = Math.DivRem(f, q);
                Debug.Assert(v < (ulong)d.Length);
                var m = (int)v;
                ref var l = ref d[m];
                ref var k = ref d[i];
                (k, l) = (l, k);
            }
            var f2 = (uint)f;
            for (; i > 0; i--)
            {
                var q = (uint)(i + 1);
                (f2, var v) = Math.DivRem(f2, q);
                Debug.Assert(v < (uint)d.Length);
                var m = (int)v;
                ref var l = ref d[m];
                ref var k = ref d[i];
                (k, l) = (l, k);
            }
        }

        internal static void ShuffleAnySmall<T>(Span<T> d, uint f2)
        {
            var i = d.Length - 1;
            for (; i > 0; i--)
            {
                var q = (uint)(i + 1);
                (f2, var v) = Math.DivRem(f2, q);
                Debug.Assert(v < (uint)d.Length);
                var m = (int)v;
                ref var l = ref d[m];
                ref var k = ref d[i];
                (k, l) = (l, k);
            }
        }

        internal static void Shuffle2<T>(Span<T> d, bool swap)
        {
            if (swap)
            {
                ref var dr = ref MemoryMarshal.GetReference(d);
                (dr, Unsafe.Add(ref dr, 1)) = (Unsafe.Add(ref dr, 1), dr);
            }
        }

        internal static void Shuffle7<T>(Span<T> d, uint t)
        {
            Debug.Assert(d.Length >= 7);
            Debug.Assert(t < 5040);
            var y1 = (int)(t % 42u);
            t /= 42;
            var y3 = (int)(t % 20u);
            t /= 20;
            var y5 = (int)t;
            var y0 = (int)(y1 % 7u);
            y1 /= 7;
            var y2 = (int)(y3 % 5u);
            y3 /= 5;
            var y4 = (int)(y5 % 3u);
            y5 /= 3;
            ref var dr = ref MemoryMarshal.GetReference(d);
            (Unsafe.Add(ref dr, y0), Unsafe.Add(ref dr, 6)) = (Unsafe.Add(ref dr, 6), Unsafe.Add(ref dr, y0));
            (Unsafe.Add(ref dr, y1), Unsafe.Add(ref dr, 5)) = (Unsafe.Add(ref dr, 5), Unsafe.Add(ref dr, y1));
            (Unsafe.Add(ref dr, y2), Unsafe.Add(ref dr, 4)) = (Unsafe.Add(ref dr, 4), Unsafe.Add(ref dr, y2));
            (Unsafe.Add(ref dr, y3), Unsafe.Add(ref dr, 3)) = (Unsafe.Add(ref dr, 3), Unsafe.Add(ref dr, y3));
            (Unsafe.Add(ref dr, y4), Unsafe.Add(ref dr, 2)) = (Unsafe.Add(ref dr, 2), Unsafe.Add(ref dr, y4));
            (Unsafe.Add(ref dr, y5), Unsafe.Add(ref dr, 1)) = (Unsafe.Add(ref dr, 1), Unsafe.Add(ref dr, y5));
        }
    }
}
