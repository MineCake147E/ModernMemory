using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    public static class BufferUtils
    {

        public static nuint CalculatePartitionSizeClass(nuint size)
        {
            var mask = size < 16 ? 1 : 0;
            nuint res = size;
            var l = BitOperations.LeadingZeroCount(size | 64u) + mask;
            var k = (nuint)1 << (-l - 3);
            res += k - 1;
            k = ~k + 1;
            res &= k;
            return res;
        }

        public static nuint CalculatePartitionSizeClassIndex(nuint size, out nuint sizeClass)
        {
            var mask = size < 16 ? 1 : 0;
            nuint res = size;
            var l = BitOperations.LeadingZeroCount(size | 64u) + mask;
            var k = (nuint)1 << (-l - 3);
            res += k - 1;
            k = ~k + 1;
            k &= res;
            var v = res < size ? (nuint)1 : 0;
            k |= ~v + 1;
            res |= ~v + 1;
            sizeClass = k;
            var m = res < 64 ? 1 : 0;
            var e = BitOperations.LeadingZeroCount((nuint)0) - 3 - BitOperations.LeadingZeroCount(res | 32);
            res >>= e + m;
            return (res & 3) + (((nuint)e - 3) << 2) + v;
        }

        public static nuint CalculatePartitionSizeClassFromIndex(nuint index)
        {
            var exponent = index >> 2;
            var h = exponent > 0 ? (nuint)0b1 : 0;
            exponent += 3 + (h ^ 1);
            var mantissa = h * 4 + (index & 3);
            mantissa <<= (int)exponent;
            if (index == 0) mantissa = 8;
            if (mantissa == 0) mantissa = nuint.MaxValue;
            return mantissa;
        }
    }
}
