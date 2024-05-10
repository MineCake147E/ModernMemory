using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public static partial class NativeMemoryUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void Fill<T>(T value, ref T destination, nuint length)
        {
            if (length < 2)
            {
                if (length > 0) destination = value;
                return;
            }
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                FillReference(value, ref destination, length);
            }
            else
            {
                switch (default(T))
                {
                    case float _:
                        FillUnmanaged(Unsafe.As<T, float>(ref value), ref Unsafe.As<T, float>(ref destination), length);
                        return;
                    case double _:
                        FillUnmanaged(Unsafe.As<T, double>(ref value), ref Unsafe.As<T, double>(ref destination), length);
                        return;
                }
                if (Vector.IsHardwareAccelerated)
                {
                    if (Unsafe.SizeOf<T>() == 1)
                    {
                        FillByVector(new(Unsafe.As<T, byte>(ref value)), ref Unsafe.As<T, byte>(ref destination), length);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() == 2)
                    {
                        FillByVector(new(Unsafe.As<T, ushort>(ref value)), ref Unsafe.As<T, ushort>(ref destination), length);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() == 3)
                    {
                        Fill3BytesVectorized(ref Unsafe.As<T, byte>(ref value), ref Unsafe.As<T, byte>(ref destination), length);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() == 4)
                    {
                        FillByVector(new(Unsafe.As<T, uint>(ref value)), ref Unsafe.As<T, uint>(ref destination), length);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() == 5)
                    {
                        Fill5BytesVectorized(ref Unsafe.As<T, byte>(ref value), ref Unsafe.As<T, byte>(ref destination), length);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() == 8)
                    {
                        FillByVector(new(Unsafe.As<T, ulong>(ref value)), ref Unsafe.As<T, ulong>(ref destination), length);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() == 16)
                    {
                        FillVector4(Unsafe.As<T, Vector4>(ref value), ref Unsafe.As<T, Vector4>(ref destination), length);
                        return;
                    }
                    else if (Unsafe.SizeOf<T>() is > 16 && Unsafe.SizeOf<T>() == Vector<byte>.Count)
                    {
                        FillVectorFit(Unsafe.As<T, Vector<byte>>(ref value), ref Unsafe.As<T, Vector<byte>>(ref destination), length);
                        return;
                    }
                }
                FillNBytes(ref Unsafe.As<T, byte>(ref value), (nuint)Unsafe.SizeOf<T>(), ref Unsafe.As<T, byte>(ref destination), length * (nuint)Unsafe.SizeOf<T>());
            }
        }

        #region Reference Types
        private static void FillReference<T>(T value, ref T destination, nuint length)
        {
            nuint i = 0;
            if (length >= 8)
            {
                var olen = length & unchecked((nuint)(nint)(-8));
                do
                {
                    ref var d = ref Unsafe.Add(ref destination, i);
                    Unsafe.Add(ref d, 0) = value;
                    Unsafe.Add(ref d, 1) = value;
                    Unsafe.Add(ref d, 2) = value;
                    Unsafe.Add(ref d, 3) = value;
                    Unsafe.Add(ref d, 4) = value;
                    Unsafe.Add(ref d, 5) = value;
                    Unsafe.Add(ref d, 6) = value;
                    Unsafe.Add(ref d, 7) = value;
                    i += 8;
                } while (i < olen);
            }
            if ((length & 4) > 0)
            {
                ref var d = ref Unsafe.Add(ref destination, i);
                Unsafe.Add(ref d, 0) = value;
                Unsafe.Add(ref d, 1) = value;
                Unsafe.Add(ref d, 2) = value;
                Unsafe.Add(ref d, 3) = value;
                i += 4;
            }
            if ((length & 2) > 0)
            {
                ref var d = ref Unsafe.Add(ref destination, i);
                Unsafe.Add(ref d, 0) = value;
                Unsafe.Add(ref d, 1) = value;
                i += 2;
            }
            if ((length & 1) > 0)
            {
                Unsafe.Add(ref destination, i) = value;
            }
        }
        #endregion

        #region Standard VectorFill
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static void FillUnmanaged<T>(T value, ref T rdi, nuint length) where T : unmanaged
        {
            nuint i = 0;
            nuint olen;
            if (length >= (nuint)Vector<T>.Count && Vector.IsHardwareAccelerated)
            {
                var vv = new Vector<T>(value);
                olen = MathUtils.CalculateUnrollingOffsetLength(length, 8 * Vector<T>.Count);
                ref var rdx = ref Unsafe.Add(ref rdi, 7 * (nuint)Vector<T>.Count);
                for (; i < olen; i += 8 * (nuint)Vector<T>.Count)
                {
                    vv.StoreUnsafe(ref rdx, i - (7 - 0) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 1) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 2) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 3) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 4) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 5) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 6) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 7) * (nuint)Vector<T>.Count);
                }

                rdx = ref Unsafe.Add(ref rdi, (nuint)Vector<T>.Count);
                olen = MathUtils.CalculateUnrollingOffsetLength(length, 2 * Vector<T>.Count);
                for (; i < olen; i += 2 * (nuint)Vector<T>.Count)
                {
                    vv.StoreUnsafe(ref rdx, i - 1 * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - 0 * (nuint)Vector<T>.Count);
                }

                olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector<T>.Count);
                if (olen < length && i < olen)
                {
                    vv.StoreUnsafe(ref rdi, i + 0 * (nuint)Vector<T>.Count);
                }
                vv.StoreUnsafe(ref rdi, length - 1 * (nuint)Vector<T>.Count);
            }
            else
            {
                for (; i < length; i++)
                {
                    Unsafe.Add(ref rdi, i) = value;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static void FillByVector<T>(Vector<T> vv, ref T rdi, nuint length) where T : unmanaged
        {
            nuint i = 0;
            nuint olen;
            if (length >= (nuint)Vector<T>.Count)
            {
                olen = MathUtils.CalculateUnrollingOffsetLength(length, 8 * Vector<T>.Count);
                ref var rdx = ref Unsafe.Add(ref rdi, 7 * (nuint)Vector<T>.Count);
                for (; i < olen; i += 8 * (nuint)Vector<T>.Count)
                {
                    vv.StoreUnsafe(ref rdx, i - (7 - 0) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 1) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 2) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 3) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 4) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 5) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 6) * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - (7 - 7) * (nuint)Vector<T>.Count);
                }

                rdx = ref Unsafe.Add(ref rdi, (nuint)Vector<T>.Count);
                olen = MathUtils.CalculateUnrollingOffsetLength(length, 2 * Vector<T>.Count);
                for (; i < olen; i += 2 * (nuint)Vector<T>.Count)
                {
                    vv.StoreUnsafe(ref rdx, i - 1 * (nuint)Vector<T>.Count);
                    vv.StoreUnsafe(ref rdx, i - 0 * (nuint)Vector<T>.Count);
                }

                olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector<T>.Count);
                if (olen < length && i < olen)
                {
                    vv.StoreUnsafe(ref rdi, i + 0 * (nuint)Vector<T>.Count);
                }
                vv.StoreUnsafe(ref rdi, length - 1 * (nuint)Vector<T>.Count);
            }
            else
            {
                var value = vv[0];
                for (; i < length; i++)
                {
                    Unsafe.Add(ref rdi, i) = value;
                }
            }
        }

        #endregion

        #region FastFill for Larger or NPOT sizes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FillVectorFit(Vector<byte> value, ref Vector<byte> rdi, nuint length)
        {
            nuint i = 0;
            nuint olen;
            var vv = value;
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 8);
            ref var rdx = ref Unsafe.Add(ref rdi, 7);
            for (; i < olen; i += 8)
            {
                ref var r8 = ref Unsafe.Add(ref rdx, i);
                Unsafe.Subtract(ref r8, 7) = vv;
                Unsafe.Subtract(ref r8, 6) = vv;
                Unsafe.Subtract(ref r8, 5) = vv;
                Unsafe.Subtract(ref r8, 4) = vv;
                Unsafe.Subtract(ref r8, 3) = vv;
                Unsafe.Subtract(ref r8, 2) = vv;
                Unsafe.Subtract(ref r8, 1) = vv;
                Unsafe.Subtract(ref r8, 0) = vv;
            }

            rdx = ref Unsafe.Add(ref rdi, 1);
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 2);
            for (; i < olen; i += 2)
            {
                ref var r8 = ref Unsafe.Add(ref rdx, i);
                Unsafe.Subtract(ref r8, 1) = vv;
                Unsafe.Subtract(ref r8, 0) = vv;
            }

            if (i < length)
            {
                Unsafe.Add(ref rdi, i) = vv;
            }
        }

        #region Vector128
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FillVector4(Vector4 value, ref Vector4 rdi, nuint length)
        {
            if (Vector256.IsHardwareAccelerated)
            {
                FillVector256(value, ref rdi, length);
                return;
            }
            FillVector128(value, ref rdi, length);
        }

        private static void FillVector256(Vector4 value, ref Vector4 x9, nuint length)
        {
            nuint i = 0, j = 0;
            nuint olen;
            var vv128 = value.AsVector128().AsByte();
            var vv = Vector256.Create(vv128, vv128);
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 16);
            ref var rdi = ref Unsafe.As<Vector4, byte>(ref x9);
            for (; i < olen; i += 16, j += 8 * (nuint)Vector256<byte>.Count)
            {
                vv.StoreUnsafe(ref rdi, j + 0 * (nuint)Vector256<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 1 * (nuint)Vector256<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 2 * (nuint)Vector256<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 3 * (nuint)Vector256<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 4 * (nuint)Vector256<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 5 * (nuint)Vector256<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 6 * (nuint)Vector256<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 7 * (nuint)Vector256<byte>.Count);
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 4);
            for (; i < olen; i += 4, j += 2 * (nuint)Vector256<byte>.Count)
            {
                vv.StoreUnsafe(ref rdi, j + 0 * (nuint)Vector256<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 1 * (nuint)Vector256<byte>.Count);
            }
            for (; i < length; i++, j += (nuint)Vector128<byte>.Count)
            {
                vv128.StoreUnsafe(ref rdi, j);
            }
        }

        private static void FillVector128(Vector4 value, ref Vector4 x9, nuint length)
        {
            nuint i = 0, j = 0;
            nuint olen;
            var vv = value.AsVector128().AsByte();
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 8);
            ref var rdi = ref Unsafe.As<Vector4, byte>(ref x9);
            for (; i < olen; i += 8, j += 8 * (nuint)Vector128<byte>.Count)
            {
                vv.StoreUnsafe(ref rdi, j + 0 * (nuint)Vector128<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 1 * (nuint)Vector128<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 2 * (nuint)Vector128<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 3 * (nuint)Vector128<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 4 * (nuint)Vector128<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 5 * (nuint)Vector128<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 6 * (nuint)Vector128<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 7 * (nuint)Vector128<byte>.Count);
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 2);
            for (; i < olen; i += 2, j += 2 * (nuint)Vector128<byte>.Count)
            {
                vv.StoreUnsafe(ref rdi, j + 0 * (nuint)Vector128<byte>.Count);
                vv.StoreUnsafe(ref rdi, j + 1 * (nuint)Vector128<byte>.Count);
            }
            for (; i < length; i++, j += (nuint)Vector128<byte>.Count)
            {
                vv.StoreUnsafe(ref rdi, j);
            }
        }

        #endregion

        #region UInt24
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Fill3BytesVectorized(ref byte value, ref byte dst, nuint u24length)
        {
            if (Vector<byte>.Count == Vector256<byte>.Count && Avx2.IsSupported)
            {
                Fill3BytesAvx2(ref value, ref dst, u24length);
                return;
            }
            if (Vector<byte>.Count == Vector128<byte>.Count && Vector128.IsHardwareAccelerated)
            {
                Fill3BytesV128(ref value, ref dst, u24length);
                return;
            }
            FillNBytes(ref value, 3, ref dst, u24length * 3);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Fill3BytesV128(ref byte value, ref byte dst, nuint u24length)
        {
            nuint i = 0, length = u24length * 3;
            var v0_16b = Vector128.Create((byte)0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0);
            var v1_16b = Vector128.Create((byte)1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1);
            var v2_16b = Vector128.Create((byte)2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2);
            var v3_16b = Vector128.CreateScalarUnsafe(Unsafe.As<byte, ushort>(ref value)).AsByte();
            v3_16b = v3_16b.WithElement(2, Unsafe.Add(ref value, 2));
            v0_16b = Vector128.Shuffle(v3_16b, v0_16b);
            v1_16b = Vector128.Shuffle(v3_16b, v1_16b);
            v2_16b = Vector128.Shuffle(v3_16b, v2_16b);
            var v0_ns = v0_16b;
            var v1_ns = v1_16b;
            var v2_ns = v2_16b;
            var v3_ns = v0_ns;
            var v4_ns = v1_ns;
            var olen = MathUtils.CalculateUnrollingOffsetLength(length, 8 * Vector128<byte>.Count);
            for (; i < olen; i += 8 * (nuint)Vector128<byte>.Count)
            {
                v0_ns.StoreUnsafe(ref dst, i + 0 * (nuint)Vector128<byte>.Count);
                v1_ns.StoreUnsafe(ref dst, i + 1 * (nuint)Vector128<byte>.Count);
                v2_ns.StoreUnsafe(ref dst, i + 2 * (nuint)Vector128<byte>.Count);
                v0_ns.StoreUnsafe(ref dst, i + 3 * (nuint)Vector128<byte>.Count);
                v0_ns = v2_ns;
                v1_ns.StoreUnsafe(ref dst, i + 4 * (nuint)Vector128<byte>.Count);
                v1_ns = v3_ns;
                v2_ns.StoreUnsafe(ref dst, i + 5 * (nuint)Vector128<byte>.Count);
                v2_ns = v4_ns;
                v3_ns.StoreUnsafe(ref dst, i + 6 * (nuint)Vector128<byte>.Count);
                v3_ns = v0_ns;
                v4_ns.StoreUnsafe(ref dst, i + 7 * (nuint)Vector128<byte>.Count);
                v4_ns = v1_ns;
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 2 * Vector128<byte>.Count);
            for (; i < olen; i += 2 * (nuint)Vector128<byte>.Count)
            {
                v0_ns = v2_ns;
                v1_ns = v3_ns;
                v2_ns = v4_ns;
                v3_ns.StoreUnsafe(ref dst, i + 0 * (nuint)Vector128<byte>.Count);
                v3_ns = v0_ns;
                v4_ns.StoreUnsafe(ref dst, i + 1 * (nuint)Vector128<byte>.Count);
                v4_ns = v1_ns;
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector128<byte>.Count);
            if (olen < length && i < olen)
            {
                v3_ns.StoreUnsafe(ref dst, i);
                i += (nuint)Vector128<byte>.Count;
            }
            var remaining = length - i;
            if (remaining == 0) return;
            var t = i % 3;
            for (; i < length; i++)
            {
                Unsafe.Add(ref dst, i) = Unsafe.Add(ref value, t);
                var u = ++t - 3;
                if (u < t)
                {
                    t = u;
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Fill3BytesAvx2(ref byte value, ref byte dst, nuint u24length)
        {
            nuint i = 0, length = u24length * 3;
            var ymm0 = Vector256.Create((byte)0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1);
            var ymm1 = Vector256.Create((byte)2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0);
            var ymm2 = Vector256.Create((byte)1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2, 0, 1, 2);
            var xmm3 = Vector128.CreateScalarUnsafe(Unsafe.As<byte, ushort>(ref value)).AsByte();
            xmm3 = xmm3.WithElement(2, Unsafe.Add(ref value, 2));
            var ymm3 = Vector256.Create(xmm3, xmm3);
            ymm0 = Avx2.Shuffle(ymm3, ymm0);
            ymm1 = Avx2.Shuffle(ymm3, ymm1);
            ymm2 = Avx2.Shuffle(ymm3, ymm2);
            ymm3 = ymm0;
            var ymm4 = ymm1;
            var olen = MathUtils.CalculateUnrollingOffsetLength(length, 8 * Vector256<byte>.Count);
            for (; i < olen; i += 8 * (nuint)Vector256<byte>.Count)
            {
                ymm0.StoreUnsafe(ref dst, i + 0 * (nuint)Vector256<byte>.Count);
                ymm1.StoreUnsafe(ref dst, i + 1 * (nuint)Vector256<byte>.Count);
                ymm2.StoreUnsafe(ref dst, i + 2 * (nuint)Vector256<byte>.Count);
                ymm0.StoreUnsafe(ref dst, i + 3 * (nuint)Vector256<byte>.Count);
                ymm0 = ymm2;
                ymm1.StoreUnsafe(ref dst, i + 4 * (nuint)Vector256<byte>.Count);
                ymm1 = ymm3;
                ymm2.StoreUnsafe(ref dst, i + 5 * (nuint)Vector256<byte>.Count);
                ymm2 = ymm4;
                ymm3.StoreUnsafe(ref dst, i + 6 * (nuint)Vector256<byte>.Count);
                ymm3 = ymm0;
                ymm4.StoreUnsafe(ref dst, i + 7 * (nuint)Vector256<byte>.Count);
                ymm4 = ymm1;
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 2 * Vector256<byte>.Count);
            for (; i < olen; i += 2 * (nuint)Vector256<byte>.Count)
            {
                ymm0 = ymm2;
                ymm1 = ymm3;
                ymm2 = ymm4;
                ymm3.StoreUnsafe(ref dst, i + 0 * (nuint)Vector256<byte>.Count);
                ymm3 = ymm0;
                ymm4.StoreUnsafe(ref dst, i + 1 * (nuint)Vector256<byte>.Count);
                ymm4 = ymm1;
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector256<byte>.Count);
            if (olen < length && i < olen)
            {
                ymm3.StoreUnsafe(ref dst, i);
                i += (nuint)Vector256<byte>.Count;
            }
            var remaining = length - i;
            if (remaining == 0) return;
            var t = i % 3;
            for (; i < length; i++)
            {
                Unsafe.Add(ref dst, i) = Unsafe.Add(ref value, t);
                var u = ++t - 3;
                if (u < t)
                {
                    t = u;
                }
            }
        }
        #endregion

        #region 5Bytes
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Fill5BytesVectorized(ref byte value, ref byte dst, nuint b5length)
        {
            if (Vector<byte>.Count == Vector256<byte>.Count && Avx2.IsSupported)
            {
                Fill5BytesAvx2(ref value, ref dst, b5length);
                return;
            }
            if (Vector<byte>.Count == Vector128<byte>.Count && Vector128.IsHardwareAccelerated)
            {
                Fill5BytesV128(ref value, ref dst, b5length);
                return;
            }
            FillNBytes(ref value, 5, ref dst, b5length * 5);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Fill5BytesV128(ref byte value, ref byte dst, nuint b5length)
        {
            nuint i = 0, length = b5length * 5;
            var v0_16b = Vector128.Create((byte)0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0);
            var v1_16b = Vector128.Create((byte)1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1);
            var v2_16b = Vector128.Create((byte)2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2);
            var v3_16b = Vector128.Create((byte)3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3);
            var v4_16b = Vector128.Create((byte)4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4);
            var v5_16b = Vector128.CreateScalarUnsafe(Unsafe.As<byte, uint>(ref value)).AsByte();
            v5_16b = v5_16b.WithElement(4, Unsafe.Add(ref value, 4));
            v0_16b = Vector128.Shuffle(v5_16b, v0_16b);
            v1_16b = Vector128.Shuffle(v5_16b, v1_16b);
            v2_16b = Vector128.Shuffle(v5_16b, v2_16b);
            v3_16b = Vector128.Shuffle(v5_16b, v3_16b);
            v4_16b = Vector128.Shuffle(v5_16b, v4_16b);
            v5_16b = v0_16b;
            var v6_16b = v1_16b;
            var v7_16b = v2_16b;
            var olen = MathUtils.CalculateUnrollingOffsetLength(length, 8 * Vector128<byte>.Count);
            for (; i < olen; i += 8 * (nuint)Vector128<byte>.Count)
            {
                v0_16b.StoreUnsafe(ref dst, i + 0 * (nuint)Vector128<byte>.Count);
                v0_16b = v3_16b;
                v1_16b.StoreUnsafe(ref dst, i + 1 * (nuint)Vector128<byte>.Count);
                v1_16b = v4_16b;
                v2_16b.StoreUnsafe(ref dst, i + 2 * (nuint)Vector128<byte>.Count);
                v2_16b = v5_16b;
                v3_16b.StoreUnsafe(ref dst, i + 3 * (nuint)Vector128<byte>.Count);
                v3_16b = v6_16b;
                v4_16b.StoreUnsafe(ref dst, i + 4 * (nuint)Vector128<byte>.Count);
                v4_16b = v7_16b;
                v5_16b.StoreUnsafe(ref dst, i + 5 * (nuint)Vector128<byte>.Count);
                v5_16b = v0_16b;
                v6_16b.StoreUnsafe(ref dst, i + 6 * (nuint)Vector128<byte>.Count);
                v6_16b = v1_16b;
                v7_16b.StoreUnsafe(ref dst, i + 7 * (nuint)Vector128<byte>.Count);
                v7_16b = v2_16b;
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 2 * Vector128<byte>.Count);
            for (; i < olen; i += 2 * (nuint)Vector128<byte>.Count)
            {
                v0_16b.StoreUnsafe(ref dst, i + 0 * (nuint)Vector128<byte>.Count);
                v0_16b = v2_16b;
                v1_16b.StoreUnsafe(ref dst, i + 1 * (nuint)Vector128<byte>.Count);
                v1_16b = v3_16b;
                v2_16b = v4_16b;
                v3_16b = v5_16b;
                v4_16b = v6_16b;
                v5_16b = v7_16b;
                v6_16b = v0_16b;
                v7_16b = v1_16b;
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector128<byte>.Count);
            if (olen < length && i < olen)
            {
                v5_16b.StoreUnsafe(ref dst, i);
                i += (nuint)Vector128<byte>.Count;
            }
            var remaining = length - i;
            if (remaining == 0) return;
            var t = i % 5;
            for (; i < length; i++)
            {
                Unsafe.Add(ref dst, i) = Unsafe.Add(ref value, t);
                var u = ++t - 5;
                if (u < t)
                {
                    t = u;
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Fill5BytesAvx2(ref byte value, ref byte dst, nuint b5length)
        {
            nuint i = 0, length = b5length * 5;
            var ymm0 = Vector256.Create((byte)0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1);
            var ymm1 = Vector256.Create((byte)2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3);
            var ymm2 = Vector256.Create((byte)4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0);
            var ymm3 = Vector256.Create((byte)1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2);
            var ymm4 = Vector256.Create((byte)3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4, 0, 1, 2, 3, 4);
            var xmm5 = Vector128.CreateScalarUnsafe(Unsafe.As<byte, uint>(ref value)).AsByte();
            xmm5 = xmm5.WithElement(4, Unsafe.Add(ref value, 4));
            var ymm5 = Vector256.Create(xmm5, xmm5);
            ymm0 = Avx2.Shuffle(ymm5, ymm0);
            ymm1 = Avx2.Shuffle(ymm5, ymm1);
            ymm2 = Avx2.Shuffle(ymm5, ymm2);
            ymm3 = Avx2.Shuffle(ymm5, ymm3);
            ymm4 = Avx2.Shuffle(ymm5, ymm4);
            ymm5 = ymm0;
            var ymm6 = ymm1;
            var ymm7 = ymm2;
            var olen = MathUtils.CalculateUnrollingOffsetLength(length, 8 * Vector256<byte>.Count);
            for (; i < olen; i += 8 * (nuint)Vector256<byte>.Count)
            {
                ymm0.StoreUnsafe(ref dst, i + 0 * (nuint)Vector256<byte>.Count);
                ymm0 = ymm3;
                ymm1.StoreUnsafe(ref dst, i + 1 * (nuint)Vector256<byte>.Count);
                ymm1 = ymm4;
                ymm2.StoreUnsafe(ref dst, i + 2 * (nuint)Vector256<byte>.Count);
                ymm2 = ymm5;
                ymm3.StoreUnsafe(ref dst, i + 3 * (nuint)Vector256<byte>.Count);
                ymm3 = ymm6;
                ymm4.StoreUnsafe(ref dst, i + 4 * (nuint)Vector256<byte>.Count);
                ymm4 = ymm7;
                ymm5.StoreUnsafe(ref dst, i + 5 * (nuint)Vector256<byte>.Count);
                ymm5 = ymm0;
                ymm6.StoreUnsafe(ref dst, i + 6 * (nuint)Vector256<byte>.Count);
                ymm6 = ymm1;
                ymm7.StoreUnsafe(ref dst, i + 7 * (nuint)Vector256<byte>.Count);
                ymm7 = ymm2;
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, 2 * Vector256<byte>.Count);
            for (; i < olen; i += 2 * (nuint)Vector256<byte>.Count)
            {
                ymm0.StoreUnsafe(ref dst, i + 0 * (nuint)Vector256<byte>.Count);
                ymm0 = ymm2;
                ymm1.StoreUnsafe(ref dst, i + 1 * (nuint)Vector256<byte>.Count);
                ymm1 = ymm3;
                ymm2 = ymm4;
                ymm3 = ymm5;
                ymm4 = ymm6;
                ymm5 = ymm7;
                ymm6 = ymm0;
                ymm7 = ymm1;
            }
            olen = MathUtils.CalculateUnrollingOffsetLength(length, Vector256<byte>.Count);
            if (olen < length && i < olen)
            {
                ymm5.StoreUnsafe(ref dst, i);
                i += (nuint)Vector256<byte>.Count;
            }
            var remaining = length - i;
            if (remaining == 0) return;
            var t = i % 5;
            for (; i < length; i++)
            {
                Unsafe.Add(ref dst, i) = Unsafe.Add(ref value, t);
                var u = ++t - 5;
                if (u < t)
                {
                    t = u;
                }
            }
        }
        #endregion

        internal static void FillNBytes(ref byte value, nuint valueLength, ref byte dst, nuint length)
        {
            nuint i = 0;
            if (length <= valueLength)
            {
                MoveMemory(ref dst, ref value, length);
                return;
            }
            MoveMemory(ref dst, ref value, valueLength);
            i += valueLength;
            while (i < length - i)
            {
                MoveMemory(ref Unsafe.Add(ref dst, i), ref dst, i);
                i += i;
            }
            MoveMemory(ref Unsafe.Add(ref dst, i), ref dst, length - i);
        }
        #endregion
    }
}
