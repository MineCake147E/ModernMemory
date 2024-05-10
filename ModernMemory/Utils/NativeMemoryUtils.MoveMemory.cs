using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
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
        #region MoveMemory

        /// <summary>
        /// Copies data from <paramref name="src"/> to <paramref name="dst"/> with specified <paramref name="length"/>.
        /// </summary>
        /// <param name="dst">The reference to the destination memory region.</param>
        /// <param name="src">The reference to the source memory region.</param>
        /// <param name="length">The length in bytes to copy.</param>
        /// <typeparam name="T">The type of data to copy.</typeparam>
        public static void MoveMemory<T>(ref T dst, ref readonly T src, nuint length)
        {
            if (length == 0 || Unsafe.AreSame(ref dst, ref Unsafe.AsRef(in src))) return;
            if (length == 1)
            {
                dst = src;
                return;
            }
#if !DEBUG
            if (length <= int.MaxValue)
            {
                var len = (int)length;
                MemoryMarshal.CreateReadOnlySpan(in src, len).CopyTo(MemoryMarshal.CreateSpan(ref dst, len));
                return;
            }
#endif
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                MoveReference(ref dst, in src, length);
            }
            else
            {
                var length1 = checked(length * (nuint)Unsafe.SizeOf<T>());   // We can't have memory more than nuint.MaxValue bytes in the first place
                MoveMemory(ref Unsafe.As<T, byte>(ref dst), in As<T, byte>(in src), length1);
            }
        }

        /// <summary>
        /// Copies data from <paramref name="src"/> to <paramref name="dst"/> with specified <paramref name="length"/>.
        /// </summary>
        /// <param name="dst">The reference to the destination memory region.</param>
        /// <param name="src">The reference to the source memory region.</param>
        /// <param name="length">The length in bytes to copy.</param>
        public static void MoveMemory(ref byte dst, ref readonly byte src, nuint length)
        {
            //check for overlap
            if (length == 0 || Unsafe.AreSame(ref dst, ref Unsafe.AsRef(in src))) return;
            if (length <= int.MaxValue)
            {
                var len = (int)length;
                MemoryMarshal.CreateReadOnlySpan(in src, len).CopyTo(MemoryMarshal.CreateSpan(ref dst, len));
            }
            else
            {
                unsafe
                {
                    fixed (void* pd = &dst)
                    fixed (void* ps = &src)
                    {
                        NativeMemory.Copy(ps, pd, length);
                    }
                }
            }
        }

        internal static void MoveReference<T>(ref T dst, ref readonly T src, nuint length, int chunkSize = int.MaxValue)
        {
            var bytes = checked((nuint)Unsafe.SizeOf<T>() * length);
            var k = Unsafe.ByteOffset(in src, in dst);
            if ((nuint)k >= bytes)
            {
                nuint i = 0;
                while (i < length)
                {
                    var len = (int)nuint.Min((nuint)chunkSize, length - i);
                    MemoryMarshal.CreateReadOnlySpan(in Add(in src, i), len).CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref dst, i), len));
                    i += (uint)len;
                }
            }
            else
            {
                var pos = length;
                while (pos > (nuint)chunkSize)
                {
                    pos -= (nuint)chunkSize;
                    MemoryMarshal.CreateReadOnlySpan(in Add(in src, pos), chunkSize).CopyTo(MemoryMarshal.CreateSpan(ref Unsafe.Add(ref dst, pos), chunkSize));
                }
                Debug.Assert(pos <= (nuint)chunkSize);
                MemoryMarshal.CreateReadOnlySpan(in src, (int)pos).CopyTo(MemoryMarshal.CreateSpan(ref dst, (int)pos));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static nuint MaxBlockSizeInBytes() => (nuint)(sizeof(uint) switch
        {
            > 0 when Vector512.IsHardwareAccelerated || Avx512F.IsSupported => Vector512<byte>.Count,
            > 0 when Vector256.IsHardwareAccelerated => Vector256<byte>.Count,
            > 0 when Vector128.IsHardwareAccelerated => Vector128<byte>.Count,
            > 0 when Vector64.IsHardwareAccelerated => Vector64<byte>.Count,
            _ => Unsafe.SizeOf<nuint>(),
        });

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void MoveMemoryConstant(ref byte dst, ref readonly byte src, [ConstantExpected] uint length)
        {
            if (length <= 4 * MaxBlockSizeInBytes() && X86Base.IsSupported)
            {
                var u = (uint)Unsafe.BitCast<bool, byte>((length & (length - 1)) > 0);
                var t = (uint)-BitOperations.LeadingZeroCount(length);
                t = t + t + u + 64;
                var res = t switch
                {
                    2 => CopyWithSingleBlock<byte>(ref dst, in src),
                    4 => CopyWithSingleBlock<ushort>(ref dst, in src),
                    5 => Copy3Bytes(ref dst, in src),
                    6 or 7 => CopyOddWithTwoBlocks<uint>(ref dst, in src, length),
                    8 or 9 => CopyOddWithTwoBlocks8To16Byte(ref dst, in src, length),
                    10 or 11 when Vector128.IsHardwareAccelerated || Sse2.IsSupported || AdvSimd.IsSupported => CopyOddWithTwoBlocks<Vector128<byte>>(ref dst, in src, length),
                    12 or 13 when Vector256.IsHardwareAccelerated || Avx.IsSupported => CopyOddWithTwoBlocks<Vector256<byte>>(ref dst, in src, length),
                    14 or 15 or 16 or 17 or 18 when Vector512.IsHardwareAccelerated || Avx512F.IsSupported => CopyWith4BlocksAvx512(ref dst, in src, length),
                    14 or 15 or 16 when Vector256.IsHardwareAccelerated || Avx.IsSupported => CopyWith4Blocks<Vector256<byte>, Vector256<byte>>(ref dst, in src, length),
                    12 or 13 or 14 when Vector128.IsHardwareAccelerated || Sse2.IsSupported || AdvSimd.IsSupported => CopyWith4Blocks<Vector128<byte>, Vector128<byte>>(ref dst, in src, length),
                    10 or 11 or 12 when Unsafe.SizeOf<nuint>() >= sizeof(ulong) => CopyWith4Blocks<ulong, ulong>(ref dst, in src, length),
                    10 or 11 or 12 when Vector64.IsHardwareAccelerated => CopyWith4Blocks<Vector64<byte>, Vector64<byte>>(ref dst, in src, length),
                    _ => t == 0,
                };
                if (res) return;
            }
            MoveMemory(ref dst, in src, length);
            return;
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            static bool CopyWithSingleBlock<T>(ref byte dst, ref readonly byte src) where T : unmanaged
            {
                var v0 = Unsafe.ReadUnaligned<T>(in src);
                Unsafe.WriteUnaligned(ref dst, v0);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            static bool CopyOddWithTwoBlocks<T>(ref byte dst, ref readonly byte src, nuint length) where T : unmanaged
                => CopyWith2Blocks<T, T>(ref dst, in src, length);

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            static bool CopyOddWithTwoBlocks8To16Byte(ref byte dst, ref readonly byte src, nuint length) => Unsafe.SizeOf<nuint>() switch
            {
                > 0 when length == 16 && Vector128.IsHardwareAccelerated => CopyWithSingleBlock<Vector128<byte>>(ref dst, in src),
                >= sizeof(ulong) => CopyWith2Blocks<ulong, ulong>(ref dst, in src, length),
                < sizeof(ulong) when Sse2.IsSupported || AdvSimd.IsSupported || Vector64.IsHardwareAccelerated => CopyWith2Blocks<double, double>(ref dst, in src, length),
                _ => CopyWith4Blocks<uint, uint>(ref dst, in src, length),
            };

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            static bool Copy3Bytes(ref byte dst, ref readonly byte src)
            {
                var v0 = Unsafe.ReadUnaligned<ushort>(in src);
                var v1 = Unsafe.ReadUnaligned<ushort>(in Add(in src, 1));
                Unsafe.WriteUnaligned(ref dst, v0);
                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 1), v1);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            static bool CopyWith2Blocks<T0, TLast>(ref byte dst, ref readonly byte src, nuint length) where T0 : unmanaged where TLast : unmanaged
            {
                var offset = length - (nuint)Unsafe.SizeOf<TLast>();
                var v0 = Unsafe.ReadUnaligned<T0>(in src);
                if (length != (nuint)Unsafe.SizeOf<T0>())
                {
                    var v1 = Unsafe.ReadUnaligned<TLast>(in Add(in src, offset));
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, offset), v1);
                }
                Unsafe.WriteUnaligned(ref dst, v0);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            static bool CopyWith4Blocks<T0, TLast>(ref byte dst, ref readonly byte src, nuint length) where T0 : unmanaged where TLast : unmanaged
            {
                var offset = length - (nuint)Unsafe.SizeOf<TLast>();
                var v0 = Unsafe.ReadUnaligned<T0>(in src);
                if (length != (nuint)Unsafe.SizeOf<T0>())
                {
                    var v3 = Unsafe.ReadUnaligned<TLast>(in Add(in src, offset));
                    if (offset > (nuint)Unsafe.SizeOf<T0>())
                    {
                        var v2 = Unsafe.ReadUnaligned<T0>(in Add(in src, 1 * (nuint)Unsafe.SizeOf<T0>()));
                        if (offset > 2 * (nuint)Unsafe.SizeOf<T0>())
                        {
                            var v1 = Unsafe.ReadUnaligned<T0>(in Add(in src, 2 * (nuint)Unsafe.SizeOf<T0>()));
                            Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 2 * Unsafe.SizeOf<T0>()), v1);
                        }
                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, Unsafe.SizeOf<T0>()), v2);
                    }
                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, offset), v3);
                }
                Unsafe.WriteUnaligned(ref dst, v0);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            static bool CopyWith4BlocksAvx512(ref byte dst, ref readonly byte src, nuint length)
            {
                var offset = length - 64;
                var v0 = Unsafe.ReadUnaligned<Vector512<byte>>(in src);
                switch (((length - 1) & 63) / 16)
                {
                    case 0:
                        {
                            offset += 64 - 16;
                            var v3 = Unsafe.ReadUnaligned<Vector128<byte>>(in Add(in src, offset));
                            if (offset > 64)
                            {
                                var v2 = Unsafe.ReadUnaligned<Vector512<byte>>(in Add(in src, 1 * 64));
                                if (offset > 2 * 64)
                                {
                                    var v1 = Unsafe.ReadUnaligned<Vector512<byte>>(in Add(in src, 2 * 64));
                                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 2 * 64), v1);
                                }
                                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 64), v2);
                            }
                            Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, offset), v3);
                            Unsafe.WriteUnaligned(ref dst, v0);
                        }
                        return true;
                    case 1:
                        {
                            offset += 64 - 32;
                            var v3 = Unsafe.ReadUnaligned<Vector256<byte>>(in Add(in src, offset));
                            if (offset > 64)
                            {
                                var v2 = Unsafe.ReadUnaligned<Vector512<byte>>(in Add(in src, 1 * 64));
                                if (offset > 2 * 64)
                                {
                                    var v1 = Unsafe.ReadUnaligned<Vector512<byte>>(in Add(in src, 2 * 64));
                                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 2 * 64), v1);
                                }
                                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 64), v2);
                            }
                            Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, offset), v3);
                            Unsafe.WriteUnaligned(ref dst, v0);
                        }
                        return true;
                    default:
                        {
                            if (length != 64)
                            {
                                var v3 = Unsafe.ReadUnaligned<Vector512<byte>>(in Add(in src, offset));
                                if (offset > 64)
                                {
                                    var v2 = Unsafe.ReadUnaligned<Vector512<byte>>(in Add(in src, 1 * 64));
                                    if (offset > 2 * 64)
                                    {
                                        var v1 = Unsafe.ReadUnaligned<Vector512<byte>>(in Add(in src, 2 * 64));
                                        Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 2 * 64), v1);
                                    }
                                    Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, 64), v2);
                                }
                                Unsafe.WriteUnaligned(ref Unsafe.Add(ref dst, offset), v3);
                            }
                            Unsafe.WriteUnaligned(ref dst, v0);
                        }
                        return true;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static bool IsKnownConstant(nuint length) => Vector128.IsHardwareAccelerated && Vector128.CreateScalarUnsafe((uint)(length + length + 1)).GetElement(1) != 0;
        #region CopyFromHead

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal static void CopyFromHead(ref byte dst, ref readonly byte src, nuint length)
        {
            // src is inside destination: copying from head
            nuint i = 0;
            if (Avx512F.IsSupported || Vector512.IsHardwareAccelerated)
            {
                CopyFromHeadVector512(ref dst, in src, length);
                return;
            }
            else if (Avx.IsSupported || Vector256.IsHardwareAccelerated)
            {
                CopyFromHeadVector256(ref dst, in src, length);
                return;
            }
            else if (Sse.IsSupported || AdvSimd.IsSupported || Vector128.IsHardwareAccelerated)
            {
                CopyFromHeadVector128(ref dst, in src, length);
                return;
            }
            else if (Vector.IsHardwareAccelerated)
            {
                i = CopyFromHeadWithBlocks<Vector<byte>>(ref dst, in src, length, 0);
            }
            i = Unsafe.SizeOf<nuint>() switch
            {
                < sizeof(ulong) when Sse2.IsSupported || AdvSimd.IsSupported || Vector64.IsHardwareAccelerated => CopyFromHeadWithBlocks<double>(ref dst, in src, length, i),
                _ => CopyFromHeadWithBlocks<nuint>(ref dst, in src, length, i),
            };
            CopyFromHeadSmall(ref dst, in src, length, i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static void CopyFromHeadSmall(ref byte dst, ref readonly byte src, nuint length, nuint i)
        {
            for (; i < length; i++)
            {
                ref var x10 = ref Unsafe.Add(ref dst, i);
                var x11 = Unsafe.ReadUnaligned<byte>(in Add(in src, i));
                Unsafe.WriteUnaligned(ref x10, x11);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static void CopyFromHeadVector512(ref byte dst, ref readonly byte src, nuint length)
        {
            nuint olen;
            nuint i = 0;
            if (length >= 256)
            {
                unsafe
                {
                    var m = 64 - (nuint)Unsafe.AsPointer(ref dst) & 63;
                    CopyFromHeadSmall(ref dst, in src, m, 0);
                    src = ref Add(in src, m);
                    dst = ref Unsafe.Add(ref dst, m);
                    length -= m;
                }
            }
            if (length >= (nuint)Vector512<byte>.Count)
            {
                if (length >= (nuint)Vector512<byte>.Count * 8)
                {
                    olen = length & unchecked(0 - (nuint)Vector512<byte>.Count * 8);
                    do
                    {
                        var v0_nb = Vector512.LoadUnsafe(in src, i + 0 * (nuint)Vector512<byte>.Count);
                        var v1_nb = Vector512.LoadUnsafe(in src, i + 1 * (nuint)Vector512<byte>.Count);
                        var v2_nb = Vector512.LoadUnsafe(in src, i + 2 * (nuint)Vector512<byte>.Count);
                        var v3_nb = Vector512.LoadUnsafe(in src, i + 3 * (nuint)Vector512<byte>.Count);
                        v0_nb.StoreUnsafe(ref dst, i + 0 * (nuint)Vector512<byte>.Count);
                        v1_nb.StoreUnsafe(ref dst, i + 1 * (nuint)Vector512<byte>.Count);
                        v2_nb.StoreUnsafe(ref dst, i + 2 * (nuint)Vector512<byte>.Count);
                        v3_nb.StoreUnsafe(ref dst, i + 3 * (nuint)Vector512<byte>.Count);
                        v0_nb = Vector512.LoadUnsafe(in src, i + 4 * (nuint)Vector512<byte>.Count);
                        v1_nb = Vector512.LoadUnsafe(in src, i + 5 * (nuint)Vector512<byte>.Count);
                        v2_nb = Vector512.LoadUnsafe(in src, i + 6 * (nuint)Vector512<byte>.Count);
                        v3_nb = Vector512.LoadUnsafe(in src, i + 7 * (nuint)Vector512<byte>.Count);
                        v0_nb.StoreUnsafe(ref dst, i + 4 * (nuint)Vector512<byte>.Count);
                        v1_nb.StoreUnsafe(ref dst, i + 5 * (nuint)Vector512<byte>.Count);
                        v2_nb.StoreUnsafe(ref dst, i + 6 * (nuint)Vector512<byte>.Count);
                        v3_nb.StoreUnsafe(ref dst, i + 7 * (nuint)Vector512<byte>.Count);
                        i += (nuint)Vector512<byte>.Count * 8;
                    } while (i < olen);
                    if (olen == length) return;
#pragma warning disable S907 // "goto" statement should not be used
                    if ((length & unchecked((nuint)Vector512<byte>.Count * 8 - (nuint)Vector512<byte>.Count)) == 0) goto UnrollFinish;
#pragma warning restore S907 // "goto" statement should not be used
                }
                olen = length & unchecked(0 - (nuint)Vector512<byte>.Count);
                do
                {
                    var v0_nb = Vector512.LoadUnsafe(in src, i);
                    v0_nb.StoreUnsafe(ref Unsafe.Add(ref dst, i));
                    i += (nuint)Vector512<byte>.Count;
                } while (i < olen);
                if (olen == length) return;
            }
            UnrollFinish:
            CopyFromHeadSmall(ref dst, in src, length, i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static void CopyFromHeadVector256(ref byte dst, ref readonly byte src, nuint length)
        {
            nuint olen;
            nuint i = 0;
            if (length >= (nuint)Vector128<byte>.Count)
            {
                if (length >= (nuint)Vector256<byte>.Count * 4)
                {
                    olen = length & unchecked(0 - (nuint)Vector256<byte>.Count * 4);
                    do
                    {
                        ref var x10 = ref Unsafe.Add(ref dst, i);
                        var v0_nb = Vector256.LoadUnsafe(in src, i + 0 * (nuint)Vector256<byte>.Count);
                        var v1_nb = Vector256.LoadUnsafe(in src, i + 1 * (nuint)Vector256<byte>.Count);
                        var v2_nb = Vector256.LoadUnsafe(in src, i + 2 * (nuint)Vector256<byte>.Count);
                        var v3_nb = Vector256.LoadUnsafe(in src, i + 3 * (nuint)Vector256<byte>.Count);
                        v0_nb.StoreUnsafe(ref x10, 0 * (nuint)Vector256<byte>.Count);
                        v1_nb.StoreUnsafe(ref x10, 1 * (nuint)Vector256<byte>.Count);
                        v2_nb.StoreUnsafe(ref x10, 2 * (nuint)Vector256<byte>.Count);
                        v3_nb.StoreUnsafe(ref x10, 3 * (nuint)Vector256<byte>.Count);
                        i += (nuint)Vector256<byte>.Count * 4;
                    } while (i < olen);
                    if (olen == length) return;
#pragma warning disable S907 // "goto" statement should not be used
                    if ((length & unchecked((nuint)Vector256<byte>.Count * 4 - (nuint)Vector128<byte>.Count)) == 0) goto UnrollFinish;
#pragma warning restore S907 // "goto" statement should not be used
                }
                olen = length & unchecked(0 - (nuint)Vector128<byte>.Count);
                do
                {
                    ref var x10 = ref Unsafe.Add(ref dst, i);
                    var v0_nb = Vector128.LoadUnsafe(in src, i);
                    v0_nb.StoreUnsafe(ref x10);
                    i += (nuint)Vector128<byte>.Count;
                } while (i < olen);
                if (olen == length) return;
            }
            UnrollFinish:
            CopyFromHeadSmall(ref dst, in src, length, i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static void CopyFromHeadVector128(ref byte dst, ref readonly byte src, nuint length)
        {
            nuint olen;
            nuint i = 0;
            if (length >= sizeof(ulong))
            {
                if (length >= (nuint)Vector128<byte>.Count * 4)
                {
                    olen = length & unchecked(0 - (nuint)Vector128<byte>.Count * 4);
                    do
                    {
                        ref var x10 = ref Unsafe.Add(ref dst, i);
                        var v0_nb = Vector128.LoadUnsafe(in src, i + 0 * (nuint)Vector128<byte>.Count);
                        var v1_nb = Vector128.LoadUnsafe(in src, i + 1 * (nuint)Vector128<byte>.Count);
                        var v2_nb = Vector128.LoadUnsafe(in src, i + 2 * (nuint)Vector128<byte>.Count);
                        var v3_nb = Vector128.LoadUnsafe(in src, i + 3 * (nuint)Vector128<byte>.Count);
                        v0_nb.StoreUnsafe(ref x10, 0 * (nuint)Vector128<byte>.Count);
                        v1_nb.StoreUnsafe(ref x10, 1 * (nuint)Vector128<byte>.Count);
                        v2_nb.StoreUnsafe(ref x10, 2 * (nuint)Vector128<byte>.Count);
                        v3_nb.StoreUnsafe(ref x10, 3 * (nuint)Vector128<byte>.Count);
                        i += (nuint)Vector128<byte>.Count * 4;
                    } while (i < olen);
                    if (olen == length) return;
#pragma warning disable S907 // "goto" statement should not be used
                    if ((length & unchecked((nuint)Vector128<byte>.Count * 4 - sizeof(ulong))) == 0) goto UnrollFinish;
#pragma warning restore S907 // "goto" statement should not be used
                }
                olen = length & unchecked(0 - (nuint)sizeof(ulong));
                do
                {
                    ref var x10 = ref Unsafe.Add(ref dst, i);
                    var x11 = Unsafe.ReadUnaligned<ulong>(in Add(in src, i));
                    Unsafe.WriteUnaligned(ref x10, x11);
                    i += sizeof(ulong);
                } while (i < olen);
                if (olen == length) return;
            }
            UnrollFinish:
            CopyFromHeadSmall(ref dst, in src, length, i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static nuint CopyFromHeadWithBlocks<T>(ref byte dst, ref readonly byte src, nuint length, nuint i) where T : unmanaged
        {
            var olen = MathUtils.CalculateUnrollingOffsetLength(length, (nuint)Unsafe.SizeOf<T>() * 4);
            for (; i < olen; i += (nuint)Unsafe.SizeOf<T>() * 4)
            {
                ref var x10 = ref Unsafe.Add(ref dst, i);
                var x11 = Unsafe.ReadUnaligned<T>(in Add(in src, i + 0 * (nuint)Unsafe.SizeOf<T>()));
                var x12 = Unsafe.ReadUnaligned<T>(in Add(in src, i + 1 * (nuint)Unsafe.SizeOf<T>()));
                var x13 = Unsafe.ReadUnaligned<T>(in Add(in src, i + 2 * (nuint)Unsafe.SizeOf<T>()));
                var x14 = Unsafe.ReadUnaligned<T>(in Add(in src, i + 3 * (nuint)Unsafe.SizeOf<T>()));
                WriteUnaligned(ref x10, 0, x11);
                WriteUnaligned(ref x10, 1, x12);
                WriteUnaligned(ref x10, 2, x13);
                WriteUnaligned(ref x10, 3, x14);
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, (nuint)Unsafe.SizeOf<T>());
            for (; i < olen; i += (nuint)Unsafe.SizeOf<T>())
            {
                ref var x10 = ref Unsafe.Add(ref dst, i);
                var x11 = Unsafe.ReadUnaligned<T>(in Add(in src, i));
                Unsafe.WriteUnaligned(ref x10, x11);
            }
            return i;
        }

        #endregion
        #region CopyFromTail

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal static void CopyFromTail(ref byte dst, ref readonly byte src, nuint length)
        {
            // destination.head is inside source: copying from tail
            nuint i = 0;
            if (length < (nuint)Unsafe.SizeOf<nuint>())
            {
                CopyFromTailSmall(ref dst, in src, length, i);
                return;
            }
            if (Avx512F.IsSupported || Vector512.IsHardwareAccelerated)
            {
                i = CopyFromTailVector512(ref dst, in src, length, i);
            }
            else if (Avx.IsSupported || Vector256.IsHardwareAccelerated)
            {
                i = CopyFromTailVector256(ref dst, in src, length, i);
            }
            else if (Sse.IsSupported || AdvSimd.IsSupported || Vector128.IsHardwareAccelerated)
            {
                i = CopyFromTailVector128(ref dst, in src, length, i);
            }
            else if (Vector.IsHardwareAccelerated)
            {
                i = CopyFromTailWithBlocks<Vector<byte>>(ref dst, in src, length, i);
            }
            i = Unsafe.SizeOf<nuint>() switch
            {
                < sizeof(ulong) when Sse2.IsSupported || AdvSimd.IsSupported || Vector64.IsHardwareAccelerated => CopyFromTailWithBlocks<double>(ref dst, in src, length, i),
                _ => CopyFromTailWithBlocks<nuint>(ref dst, in src, length, i),
            };
            CopyFromTailSmall(ref dst, in src, length, i);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void CopyFromTailSmall(ref byte dst, ref readonly byte src, nuint length, nuint i)
        {
            var ni = 0 - i;
            ref readonly var x8 = ref Add(in src, length - 1);
            ref var x9 = ref Unsafe.Add(ref dst, length - 1);
            for (; i < length; i++, ni--)
            {
                ref var x10 = ref Unsafe.Add(ref x9, ni);
                Unsafe.WriteUnaligned(ref x10, Unsafe.ReadUnaligned<byte>(in Add(in x8, ni)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static nuint CopyFromTailVector512(ref byte dst, ref readonly byte src, nuint length, nuint i)
        {
            var ni = 0 - i;
            ref readonly var x8 = ref Add(in src, length - (nuint)Vector512<byte>.Count);
            ref var x9 = ref Unsafe.Add(ref dst, length - (nuint)Vector512<byte>.Count);
            var olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector512<byte>.Count * 16);
            for (; i < olen; i += (nuint)Vector512<byte>.Count * 16, ni -= (nuint)Vector512<byte>.Count * 16)
            {
                ref var x11 = ref Unsafe.Add(ref x9, ni);
                var v0_nb = Vector512.LoadUnsafe(in x8, ni - 0 * (nuint)Vector512<byte>.Count);
                var v1_nb = Vector512.LoadUnsafe(in x8, ni - 1 * (nuint)Vector512<byte>.Count);
                var v2_nb = Vector512.LoadUnsafe(in x8, ni - 2 * (nuint)Vector512<byte>.Count);
                var v3_nb = Vector512.LoadUnsafe(in x8, ni - 3 * (nuint)Vector512<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 0 * Vector512<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 1 * Vector512<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 2 * Vector512<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 3 * Vector512<byte>.Count));
                v0_nb = Vector512.LoadUnsafe(in x8, ni - 4 * (nuint)Vector512<byte>.Count);
                v1_nb = Vector512.LoadUnsafe(in x8, ni - 5 * (nuint)Vector512<byte>.Count);
                v2_nb = Vector512.LoadUnsafe(in x8, ni - 6 * (nuint)Vector512<byte>.Count);
                v3_nb = Vector512.LoadUnsafe(in x8, ni - 7 * (nuint)Vector512<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 4 * Vector512<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 5 * Vector512<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 6 * Vector512<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 7 * Vector512<byte>.Count));
                v0_nb = Vector512.LoadUnsafe(in x8, ni - 8 * (nuint)Vector512<byte>.Count);
                v1_nb = Vector512.LoadUnsafe(in x8, ni - 9 * (nuint)Vector512<byte>.Count);
                v2_nb = Vector512.LoadUnsafe(in x8, ni - 10 * (nuint)Vector512<byte>.Count);
                v3_nb = Vector512.LoadUnsafe(in x8, ni - 11 * (nuint)Vector512<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 8 * Vector512<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 9 * Vector512<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 10 * Vector512<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 11 * Vector512<byte>.Count));
                v0_nb = Vector512.LoadUnsafe(in x8, ni - 12 * (nuint)Vector512<byte>.Count);
                v1_nb = Vector512.LoadUnsafe(in x8, ni - 13 * (nuint)Vector512<byte>.Count);
                v2_nb = Vector512.LoadUnsafe(in x8, ni - 14 * (nuint)Vector512<byte>.Count);
                v3_nb = Vector512.LoadUnsafe(in x8, ni - 15 * (nuint)Vector512<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 12 * Vector512<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 13 * Vector512<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 14 * Vector512<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 15 * Vector512<byte>.Count));
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector512<byte>.Count * 4);
            for (; i < olen; i += (nuint)Vector512<byte>.Count * 4, ni -= (nuint)Vector512<byte>.Count * 4)
            {
                ref var x11 = ref Unsafe.Add(ref x9, ni);
                var v0_nb = Vector512.LoadUnsafe(in x8, ni - 0 * (nuint)Vector512<byte>.Count);
                var v1_nb = Vector512.LoadUnsafe(in x8, ni - 1 * (nuint)Vector512<byte>.Count);
                var v2_nb = Vector512.LoadUnsafe(in x8, ni - 2 * (nuint)Vector512<byte>.Count);
                var v3_nb = Vector512.LoadUnsafe(in x8, ni - 3 * (nuint)Vector512<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 0 * Vector512<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 1 * Vector512<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 2 * Vector512<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 3 * Vector512<byte>.Count));
            }
            return i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static nuint CopyFromTailVector256(ref byte dst, ref readonly byte src, nuint length, nuint i)
        {
            var ni = 0 - i;
            ref readonly var x8 = ref Add(in src, length - (nuint)Vector256<byte>.Count);
            ref var x9 = ref Unsafe.Add(ref dst, length - (nuint)Vector256<byte>.Count);
            var olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector256<byte>.Count * 16);
            for (; i < olen; i += (nuint)Vector256<byte>.Count * 16, ni -= (nuint)Vector256<byte>.Count * 16)
            {
                ref var x11 = ref Unsafe.Add(ref x9, ni);
                var v0_nb = Vector256.LoadUnsafe(in x8, ni - 0 * (nuint)Vector256<byte>.Count);
                var v1_nb = Vector256.LoadUnsafe(in x8, ni - 1 * (nuint)Vector256<byte>.Count);
                var v2_nb = Vector256.LoadUnsafe(in x8, ni - 2 * (nuint)Vector256<byte>.Count);
                var v3_nb = Vector256.LoadUnsafe(in x8, ni - 3 * (nuint)Vector256<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 0 * Vector256<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 1 * Vector256<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 2 * Vector256<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 3 * Vector256<byte>.Count));
                v0_nb = Vector256.LoadUnsafe(in x8, ni - 4 * (nuint)Vector256<byte>.Count);
                v1_nb = Vector256.LoadUnsafe(in x8, ni - 5 * (nuint)Vector256<byte>.Count);
                v2_nb = Vector256.LoadUnsafe(in x8, ni - 6 * (nuint)Vector256<byte>.Count);
                v3_nb = Vector256.LoadUnsafe(in x8, ni - 7 * (nuint)Vector256<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 4 * Vector256<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 5 * Vector256<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 6 * Vector256<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 7 * Vector256<byte>.Count));
                v0_nb = Vector256.LoadUnsafe(in x8, ni - 8 * (nuint)Vector256<byte>.Count);
                v1_nb = Vector256.LoadUnsafe(in x8, ni - 9 * (nuint)Vector256<byte>.Count);
                v2_nb = Vector256.LoadUnsafe(in x8, ni - 10 * (nuint)Vector256<byte>.Count);
                v3_nb = Vector256.LoadUnsafe(in x8, ni - 11 * (nuint)Vector256<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 8 * Vector256<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 9 * Vector256<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 10 * Vector256<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 11 * Vector256<byte>.Count));
                v0_nb = Vector256.LoadUnsafe(in x8, ni - 12 * (nuint)Vector256<byte>.Count);
                v1_nb = Vector256.LoadUnsafe(in x8, ni - 13 * (nuint)Vector256<byte>.Count);
                v2_nb = Vector256.LoadUnsafe(in x8, ni - 14 * (nuint)Vector256<byte>.Count);
                v3_nb = Vector256.LoadUnsafe(in x8, ni - 15 * (nuint)Vector256<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 12 * Vector256<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 13 * Vector256<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 14 * Vector256<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 15 * Vector256<byte>.Count));
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector256<byte>.Count * 4);
            for (; i < olen; i += (nuint)Vector256<byte>.Count * 4, ni -= (nuint)Vector256<byte>.Count * 4)
            {
                ref var x11 = ref Unsafe.Add(ref x9, ni);
                var v0_nb = Vector256.LoadUnsafe(in x8, ni - 0 * (nuint)Vector256<byte>.Count);
                var v1_nb = Vector256.LoadUnsafe(in x8, ni - 1 * (nuint)Vector256<byte>.Count);
                var v2_nb = Vector256.LoadUnsafe(in x8, ni - 2 * (nuint)Vector256<byte>.Count);
                var v3_nb = Vector256.LoadUnsafe(in x8, ni - 3 * (nuint)Vector256<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 0 * Vector256<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 1 * Vector256<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 2 * Vector256<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 3 * Vector256<byte>.Count));
            }
            return i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static nuint CopyFromTailVector128(ref byte dst, ref readonly byte src, nuint length, nuint i)
        {
            var ni = 0 - i;
            ref readonly var x8 = ref Add(in src, length - (nuint)Vector128<byte>.Count);
            ref var x9 = ref Unsafe.Add(ref dst, length - (nuint)Vector128<byte>.Count);
            var olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector128<byte>.Count * 16);
            for (; i < olen; i += (nuint)Vector128<byte>.Count * 16, ni -= (nuint)Vector128<byte>.Count * 16)
            {
                ref var x11 = ref Unsafe.Add(ref x9, ni);
                var v0_nb = Vector128.LoadUnsafe(in x8, ni - 0 * (nuint)Vector128<byte>.Count);
                var v1_nb = Vector128.LoadUnsafe(in x8, ni - 1 * (nuint)Vector128<byte>.Count);
                var v2_nb = Vector128.LoadUnsafe(in x8, ni - 2 * (nuint)Vector128<byte>.Count);
                var v3_nb = Vector128.LoadUnsafe(in x8, ni - 3 * (nuint)Vector128<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 0 * Vector128<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 1 * Vector128<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 2 * Vector128<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 3 * Vector128<byte>.Count));
                v0_nb = Vector128.LoadUnsafe(in x8, ni - 4 * (nuint)Vector128<byte>.Count);
                v1_nb = Vector128.LoadUnsafe(in x8, ni - 5 * (nuint)Vector128<byte>.Count);
                v2_nb = Vector128.LoadUnsafe(in x8, ni - 6 * (nuint)Vector128<byte>.Count);
                v3_nb = Vector128.LoadUnsafe(in x8, ni - 7 * (nuint)Vector128<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 4 * Vector128<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 5 * Vector128<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 6 * Vector128<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 7 * Vector128<byte>.Count));
                v0_nb = Vector128.LoadUnsafe(in x8, ni - 8 * (nuint)Vector128<byte>.Count);
                v1_nb = Vector128.LoadUnsafe(in x8, ni - 9 * (nuint)Vector128<byte>.Count);
                v2_nb = Vector128.LoadUnsafe(in x8, ni - 10 * (nuint)Vector128<byte>.Count);
                v3_nb = Vector128.LoadUnsafe(in x8, ni - 11 * (nuint)Vector128<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 8 * Vector128<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 9 * Vector128<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 10 * Vector128<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 11 * Vector128<byte>.Count));
                v0_nb = Vector128.LoadUnsafe(in x8, ni - 12 * (nuint)Vector128<byte>.Count);
                v1_nb = Vector128.LoadUnsafe(in x8, ni - 13 * (nuint)Vector128<byte>.Count);
                v2_nb = Vector128.LoadUnsafe(in x8, ni - 14 * (nuint)Vector128<byte>.Count);
                v3_nb = Vector128.LoadUnsafe(in x8, ni - 15 * (nuint)Vector128<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 12 * Vector128<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 13 * Vector128<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 14 * Vector128<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 15 * Vector128<byte>.Count));
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector128<byte>.Count * 4);
            for (; i < olen; i += (nuint)Vector128<byte>.Count * 4, ni -= (nuint)Vector128<byte>.Count * 4)
            {
                ref var x11 = ref Unsafe.Add(ref x9, ni);
                var v0_nb = Vector128.LoadUnsafe(in x8, ni - 0 * (nuint)Vector128<byte>.Count);
                var v1_nb = Vector128.LoadUnsafe(in x8, ni - 1 * (nuint)Vector128<byte>.Count);
                var v2_nb = Vector128.LoadUnsafe(in x8, ni - 2 * (nuint)Vector128<byte>.Count);
                var v3_nb = Vector128.LoadUnsafe(in x8, ni - 3 * (nuint)Vector128<byte>.Count);
                v0_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 0 * Vector128<byte>.Count));
                v1_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 1 * Vector128<byte>.Count));
                v2_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 2 * Vector128<byte>.Count));
                v3_nb.StoreUnsafe(ref Unsafe.Subtract(ref x11, 3 * Vector128<byte>.Count));
            }
            return i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static nuint CopyFromTailWithBlocks<T>(ref byte dst, ref readonly byte src, nuint length, nuint i) where T : unmanaged
        {
            var ni = 0 - i;
            ref readonly var x8 = ref Add(in src, length - (nuint)Unsafe.SizeOf<T>());
            ref var x9 = ref Unsafe.Add(ref dst, length - (nuint)Unsafe.SizeOf<T>());
            var olen = MathUtils.CalculateUnrollingOffsetLength(length, 4 * (nuint)Unsafe.SizeOf<T>());
            for (; i < olen; i += (nuint)Unsafe.SizeOf<T>() * 4, ni -= (nuint)Unsafe.SizeOf<T>() * 4)
            {
                ref var x11 = ref Unsafe.Add(ref x9, ni);
                var x12 = Unsafe.ReadUnaligned<T>(in Add(in x8, ni - 0 * (nuint)Unsafe.SizeOf<T>()));
                var x13 = Unsafe.ReadUnaligned<T>(in Add(in x8, ni - 1 * (nuint)Unsafe.SizeOf<T>()));
                var x14 = Unsafe.ReadUnaligned<T>(in Add(in x8, ni - 2 * (nuint)Unsafe.SizeOf<T>()));
                var x15 = Unsafe.ReadUnaligned<T>(in Add(in x8, ni - 3 * (nuint)Unsafe.SizeOf<T>()));
                Unsafe.WriteUnaligned(ref Unsafe.Subtract(ref x11, 0 * (nuint)Unsafe.SizeOf<T>()), x12);
                Unsafe.WriteUnaligned(ref Unsafe.Subtract(ref x11, 1 * (nuint)Unsafe.SizeOf<T>()), x13);
                Unsafe.WriteUnaligned(ref Unsafe.Subtract(ref x11, 2 * (nuint)Unsafe.SizeOf<T>()), x14);
                Unsafe.WriteUnaligned(ref Unsafe.Subtract(ref x11, 3 * (nuint)Unsafe.SizeOf<T>()), x15);
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, (nuint)Unsafe.SizeOf<T>());
            for (; i < olen; i += (nuint)Unsafe.SizeOf<T>(), ni -= (nuint)Unsafe.SizeOf<T>())
            {
                ref var x10 = ref Unsafe.Add(ref x9, ni);
                Unsafe.WriteUnaligned(ref x10, Unsafe.ReadUnaligned<T>(in Add(in x8, ni)));
            }
            return i;
        }

        #endregion
        #endregion

        #region Custom Blocks

        #endregion
    }
}
