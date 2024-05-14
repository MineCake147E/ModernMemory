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

using ModernMemory.Threading;

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

        private static unsafe nuint ThreadStaticRandom(nuint entropy)
        {
            ref var m = ref rngState;
            var e2 = (nuint)Unsafe.AsPointer(ref m);
            e2 ^= e2 << 7;
            var c = m;
            var nv = c ^ entropy;
            e2 = (e2 & ~(nuint)3) | 1;
            nv = nv * 65537 + e2;
            m = nv;
            return c;
        }

        public static nuint IncrementRandomized(ref nuint counter)
        {
            var count = counter;
            nuint delta = 1;
            if (count > 0)
            {
                var lc = BitOperations.LeadingZeroCount((nuint)0) - 1 - BitOperations.LeadingZeroCount(counter);
                if (lc >= 13)
                {
                    delta <<= lc - 12;
                    nuint v = 0;
                    var span = MemoryMarshal.AsBytes(new Span<nuint>(ref v));
                    Random.Shared.NextBytes(span);
                    if ((v & (delta - 1)) > 0)
                    {
                        delta = 0;
                    }
                }
            }
            if (delta > 0) AtomicUtils.Add(ref counter, delta);
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
