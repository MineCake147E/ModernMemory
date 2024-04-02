using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers.DataFlow;

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
