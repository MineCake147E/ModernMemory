using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers.Pooling
{
    public static class PoolingUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CheckOccupationByIndex(nuint index, scoped ReadOnlyNativeSpan<uint> occupationBitTable)
        {
            var bitIndex = index & ((nuint)sizeof(uint) * 8 - 1);
            var arrIndex = index / (sizeof(uint) * 8);
            return arrIndex >= occupationBitTable.Length || (occupationBitTable[arrIndex] & 1u << (int)bitIndex) > 0;
        }

        public static bool TryAllocate(scoped NativeSpan<uint> occupationBitTable, out nuint index, nuint retryCount = 0)
        {
            var ocx = occupationBitTable;
            var occ = NativeMemoryUtils.Cast<uint, nuint>(ocx);
            NativeMemoryUtils.Prefetch(ref ocx.Head);
            if (retryCount == 0)
            {
                retryCount = ocx.Length * 8 * sizeof(uint);
            }
            var remainingRetries = retryCount - 1;
            var res = nuint.MaxValue;
            var i = nuint.MaxValue;
            while (remainingRetries < retryCount && res == nuint.MaxValue)
            {
                if (++i >= occ.Length)
                {
                    i -= occ.Length;
                    if (i >= occ.Length)
                    {
                        i %= occ.Length;
                    }
                }
                ref var rb = ref occ[i];
                var b = rb;
                var cb = ~b;
                var m = BitOperations.TrailingZeroCount(cb);
                if (m < Unsafe.SizeOf<nuint>() * 8)
                {
                    var k = (nuint)1u << m;
                    var ob = Interlocked.CompareExchange(ref rb, k | b, b);
                    if (ob == b)
                    {
                        res = i * (nuint)Unsafe.SizeOf<nuint>() * 8 + (uint)m;
                        break;
                    }
                    m += 1;
                }
                remainingRetries -= (uint)m;
            }
            index = res;
            return res < nuint.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryReturn(nuint index, scoped NativeSpan<uint> occupationBitTable)
        {
            var bitIndex = index & ((nuint)sizeof(uint) * 8 - 1);
            var arrIndex = index / (sizeof(uint) * 8);
            var k = ~(1u << (int)bitIndex);
            ref var mask = ref occupationBitTable[arrIndex];
            return (Interlocked.And(ref mask, k) & ~k) != 0;
        }
    }
}
