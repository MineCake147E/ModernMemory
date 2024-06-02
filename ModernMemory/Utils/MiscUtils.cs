using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public static class MiscUtils
    {
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Transfer<T>(this ref T item) where T : struct
        {
            var k = item;
            item = default;
            return k;
        }

        public static bool ReturnFalseWith<T>(T value, out T outParameter)
        {
            outParameter = value;
            return false;
        }

        [ThreadStatic]
        private static nuint rngState;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe nuint ThreadStaticRandom(nuint entropy)
        {
            ref var m = ref rngState;
            var c = m;
            var e2 = entropy + (nuint)Unsafe.AsPointer(ref m);
            e2 ^= e2 << 13;
            e2 ^= e2 >> 32;
            var nv = c;
            e2 |= 1;
            nv = nv * unchecked((nuint)6364136223846793005ul) + e2;
            m = nv;
            return c;
        }

        public static nuint IncrementRandomized(ref nuint counter, int precision = 13)
        {
            var count = counter;
            nuint delta = count < nuint.MaxValue ? 1u : 0;
            switch (count >> precision)
            {
                case > 0:
                    var lc = BitOperations.LeadingZeroCount((nuint)0) - BitOperations.LeadingZeroCount(count);
                    delta <<= lc - precision;
                    nuint v = ThreadStaticRandom(count);
                    if ((v & (delta - 1)) > 0)
                    {
                        break;
                    }
#pragma warning disable S907 // "goto" statement should not be used
                    goto default;
#pragma warning restore S907 // "goto" statement should not be used
                default:
                    AtomicUtils.Add(ref counter, delta);
                    break;
            }
            return count;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsType<TFrom, TTo>(in TFrom specimen)
            => typeof(TFrom).IsValueType ? default(TFrom) is TTo : specimen is TTo;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsRegisterFriendly<T>()
            => RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            ? !typeof(T).IsValueType || Unsafe.SizeOf<T>() == Unsafe.SizeOf<nuint>()
            : Unsafe.SizeOf<T>() switch
            {
                sizeof(byte) or sizeof(ushort) or sizeof(uint) => true,
                sizeof(ulong) => Unsafe.SizeOf<nuint>() == sizeof(ulong) || Sse.IsSupported || AdvSimd.IsSupported || Vector64.IsHardwareAccelerated,
                16 => Sse.IsSupported || AdvSimd.IsSupported || Vector128.IsHardwareAccelerated,
                32 => Avx.IsSupported || Vector256.IsHardwareAccelerated,
                64 => Avx512F.IsSupported || Vector512.IsHardwareAccelerated,
                _ => false,
            };
    }
}
