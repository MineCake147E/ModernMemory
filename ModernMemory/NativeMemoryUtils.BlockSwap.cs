using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory
{
    public static partial class NativeMemoryUtils
    {
        public static void SwapValues<T>(ref T x, ref T y, nuint length)
        {
            if (length <= 1)
            {
                if (length == 1)
                {
                    (y, x) = (x, y);
                }
                return;
            }
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                var nl = checked((nuint)Unsafe.SizeOf<T>() * length);
                SwapBytes(ref Unsafe.As<T, byte>(ref x), ref Unsafe.As<T, byte>(ref y), nl);
                return;
            }
            SwapReference(ref x, ref y, length);
        }

        private static void SwapReference<T>(ref T x, ref T y, nuint length)
        {
            Debug.Assert(RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            if (Unsafe.SizeOf<T>() * 64 <= 0x4000 && length >= 64)
            {
                SwapReferenceByBlock64(ref x, ref y, length);
                return;
            }
            if (Unsafe.SizeOf<T>() <= 1024)
            {
                SwapReferenceByBlock16(ref x, ref y, length);
                return;
            }
            SwapReferenceLargeStruct(ref x, ref y, length);
        }

        private static void SwapReferenceLargeStruct<T>(ref T x, ref T y, nuint length)
        {
            nuint i = 0;
            for (; i < length; i++)
            {
                var x0 = Unsafe.Add(ref x, i);
                var x1 = Unsafe.Add(ref y, i);
                Unsafe.Add(ref y, i) = x0;
                Unsafe.Add(ref x, i) = x1;
            }
        }

        private static void SwapReferenceByBlock64<T>(ref T x, ref T y, nuint length)
        {
            Unsafe.SkipInit(out FixedArray64<T> buf);
            SwapReferenceBuffered(ref x, ref y, length, ref buf);
        }

        private static void SwapReferenceByBlock16<T>(ref T x, ref T y, nuint length)
        {
            Unsafe.SkipInit(out FixedArray16<T> buf);
            SwapReferenceBuffered(ref x, ref y, length, ref buf);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SwapReferenceBuffered<T, TArray>(ref T x, ref T y, nuint length, ref TArray buf)
        where TArray : struct, IFixedGenericInlineArray<T, TArray>
        {
            var buffer = TArray.AsSpan(ref buf);
            if (buffer.Length > 0)
            {
                nuint i = 0;
                var ol = length - (nuint)buffer.Length + 1;
                if (ol < length)
                {
                    for (; i < ol; i += (nuint)buffer.Length)
                    {
                        var sx = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref x, i), buffer.Length);
                        var sy = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref y, i), buffer.Length);
                        sx.CopyTo(buffer);
                        sy.CopyTo(sx);
                        buffer.CopyTo(sy);
                    }
                }
                var rem = length - i;
                if (rem != 0 && rem <= length && (uint)rem <= (uint)buffer.Length)
                {
                    var ksp = buffer.Slice(0, (int)rem);
                    var sx = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref x, i), ksp.Length);
                    var sy = MemoryMarshal.CreateSpan(ref Unsafe.Add(ref y, i), ksp.Length);
                    sx.CopyTo(ksp);
                    sy.CopyTo(sx);
                    ksp.CopyTo(sy);
                }
            }
            else
            {
                _ = 1 / int.Max(0, buffer.Length);
            }
        }


        private static void SwapBytes(ref byte x, ref byte y, nuint length)
        {
            if (Vector512.IsHardwareAccelerated || Avx512F.IsSupported)
            {
                SwapBytesV512(ref x, ref y, length);
                return;
            }
            else if (Vector256.IsHardwareAccelerated || Avx.IsSupported)
            {
                SwapBytesV256(ref x, ref y, length);
                return;
            }
            else if (Vector128.IsHardwareAccelerated || Sse.IsSupported || AdvSimd.IsSupported)
            {
                SwapBytesV128(ref x, ref y, length);
                return;
            }
            SwapSmall(ref x, ref y, length, 0);
        }

        private static void SwapBytesV512(ref byte x, ref byte y, nuint length)
        {
            nuint i = 0;
            var ol = length - ((nuint)Vector512<byte>.Count * 2 - 1);
            if (ol < length)
            {
                for (; i < ol; i += (nuint)Vector512<byte>.Count * 2)
                {
                    var zmm0 = Vector512.LoadUnsafe(ref x, i + 0 * (nuint)Vector512<byte>.Count);
                    var zmm1 = Vector512.LoadUnsafe(ref x, i + 1 * (nuint)Vector512<byte>.Count);
                    var zmm2 = Vector512.LoadUnsafe(ref y, i + 0 * (nuint)Vector512<byte>.Count);
                    var zmm3 = Vector512.LoadUnsafe(ref y, i + 1 * (nuint)Vector512<byte>.Count);
                    zmm0.StoreUnsafe(ref y, i + 0 * (nuint)Vector512<byte>.Count);
                    zmm1.StoreUnsafe(ref y, i + 1 * (nuint)Vector512<byte>.Count);
                    zmm2.StoreUnsafe(ref x, i + 0 * (nuint)Vector512<byte>.Count);
                    zmm3.StoreUnsafe(ref x, i + 1 * (nuint)Vector512<byte>.Count);
                }
            }
            ol = length - ((nuint)Vector256<byte>.Count - 1);
            if (ol < length)
            {
                for (; i < ol; i += (nuint)Vector256<byte>.Count)
                {
                    var ymm0 = Vector256.LoadUnsafe(ref x, i);
                    var ymm2 = Vector256.LoadUnsafe(ref y, i);
                    ymm0.StoreUnsafe(ref y, i);
                    ymm2.StoreUnsafe(ref x, i);
                }
            }
            SwapSmall(ref x, ref y, length, i);
        }

        private static void SwapBytesV256(ref byte x, ref byte y, nuint length)
        {
            nuint i = 0;
            var ol = length - ((nuint)Vector256<byte>.Count * 2 - 1);
            if (ol < length)
            {
                for (; i < ol; i += (nuint)Vector256<byte>.Count * 2)
                {
                    var ymm0 = Vector256.LoadUnsafe(ref x, i + 0 * (nuint)Vector256<byte>.Count);
                    var ymm1 = Vector256.LoadUnsafe(ref x, i + 1 * (nuint)Vector256<byte>.Count);
                    var ymm2 = Vector256.LoadUnsafe(ref y, i + 0 * (nuint)Vector256<byte>.Count);
                    var ymm3 = Vector256.LoadUnsafe(ref y, i + 1 * (nuint)Vector256<byte>.Count);
                    ymm0.StoreUnsafe(ref y, i + 0 * (nuint)Vector256<byte>.Count);
                    ymm1.StoreUnsafe(ref y, i + 1 * (nuint)Vector256<byte>.Count);
                    ymm2.StoreUnsafe(ref x, i + 0 * (nuint)Vector256<byte>.Count);
                    ymm3.StoreUnsafe(ref x, i + 1 * (nuint)Vector256<byte>.Count);
                }
            }
            ol = length - ((nuint)Vector128<byte>.Count - 1);
            if (ol < length)
            {
                for (; i < ol; i += (nuint)Vector128<byte>.Count)
                {
                    var ymm0 = Vector128.LoadUnsafe(ref x, i);
                    var ymm2 = Vector128.LoadUnsafe(ref y, i);
                    ymm0.StoreUnsafe(ref y, i);
                    ymm2.StoreUnsafe(ref x, i);
                }
            }
            SwapSmall(ref x, ref y, length, i);
        }

        private static void SwapBytesV128(ref byte x, ref byte y, nuint length)
        {
            nuint i = 0;
            var ol = length - ((nuint)Vector128<byte>.Count * 2 - 1);
            if (ol < length)
            {
                for (; i < ol; i += (nuint)Vector128<byte>.Count * 2)
                {
                    var v0_16b = Vector128.LoadUnsafe(ref x, i + 0 * (nuint)Vector128<byte>.Count);
                    var v1_16b = Vector128.LoadUnsafe(ref x, i + 1 * (nuint)Vector128<byte>.Count);
                    var v2_16b = Vector128.LoadUnsafe(ref y, i + 0 * (nuint)Vector128<byte>.Count);
                    var v3_16b = Vector128.LoadUnsafe(ref y, i + 1 * (nuint)Vector128<byte>.Count);
                    v0_16b.StoreUnsafe(ref y, i + 0 * (nuint)Vector128<byte>.Count);
                    v1_16b.StoreUnsafe(ref y, i + 1 * (nuint)Vector128<byte>.Count);
                    v2_16b.StoreUnsafe(ref x, i + 0 * (nuint)Vector128<byte>.Count);
                    v3_16b.StoreUnsafe(ref x, i + 1 * (nuint)Vector128<byte>.Count);
                }
            }
            SwapSmall(ref x, ref y, length, i);
        }

        private static void SwapSmall(ref byte x, ref byte y, nuint length, nuint i)
        {
            var ol = length - ((nuint)Unsafe.SizeOf<nuint>() - 1);
            if (ol < length)
            {
                for (; i < ol; i += (nuint)Unsafe.SizeOf<nuint>())
                {
                    var x0 = Unsafe.ReadUnaligned<nuint>(ref Unsafe.AddByteOffset(ref x, i));
                    var x2 = Unsafe.ReadUnaligned<nuint>(ref Unsafe.AddByteOffset(ref y, i));
                    Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref y, i), x0);
                    Unsafe.WriteUnaligned(ref Unsafe.AddByteOffset(ref x, i), x2);
                }
            }
            for (; i < length; i++)
            {
                ref var xi = ref Unsafe.Add(ref x, i);
                ref var yi = ref Unsafe.Add(ref y, i);
                (xi, yi) = (yi, xi);
            }
        }
    }
}
