using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public static partial class NativeMemoryUtils
    {
        #region Extern

        #endregion

        #region Clear
        public static void Clear<T>(ref T dst, nuint length)
        {
            if (length == 0) return;
            if (length <= int.MaxValue)
            {
                MemoryMarshal.CreateSpan(ref dst, (int)length).Clear();
                return;
            }
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                nuint head = 0;
                do
                {
                    var r = length - head;
                    if (r > int.MaxValue)
                    {
                        r = int.MaxValue;
                    }
                    MemoryMarshal.CreateSpan(ref Unsafe.Add(ref dst, head), (int)r).Clear();
                    head += r;
                } while (head < length);
            }
            else
            {
                unsafe
                {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type (false positive)
                    fixed (void* a = &dst)
                    {
                        NativeMemory.Clear(a, checked(length * (nuint)Unsafe.SizeOf<T>()));
                    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void ClearIfReferenceOrContainsReferences<T>(this Span<T> span)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) span.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static void ClearIfReferenceOrContainsReferences<T>(this NativeSpan<T> span)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) span.Clear();
        }
        #endregion

        #region CreateNativeSpan
        /// <inheritdoc cref="NativeSpan{T}.NativeSpan(ref T, nuint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static NativeSpan<T> CreateNativeSpan<T>(ref T head, nuint length) => new(ref head, length);

        /// <inheritdoc cref="ReadOnlyNativeSpan{T}.ReadOnlyNativeSpan(ref T, nuint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan<T>(ref readonly T head, nuint length) => new(in head, length);
        #endregion

        #region GetReference
        /// <inheritdoc cref="MemoryMarshal.GetReference{T}(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]

        public static ref T GetReference<T>(NativeSpan<T> span) => ref span.Head;

        /// <inheritdoc cref="MemoryMarshal.GetReference{T}(ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ref T GetReference<T>(ReadOnlyNativeSpan<T> span) => ref Unsafe.AsRef(in span.Head);
        #endregion

        #region Cast

        /// <summary>
        /// Casts a <see cref="NativeSpan{T}"/> of one primitive type, <typeparamref name="T"/>, to a <c>NativeSpan&lt;byte&gt;</c>
        /// </summary>
        /// <inheritdoc cref="MemoryMarshal.AsBytes{T}(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static NativeSpan<byte> AsBytes<T>(NativeSpan<T> span) where T : unmanaged => CreateNativeSpan(ref Unsafe.As<T, byte>(ref span.Head), checked(span.Length * (nuint)Unsafe.SizeOf<T>()));

        /// <summary>
        /// Casts a <see cref="ReadOnlyNativeSpan{T}"/> of one primitive type, <typeparamref name="T"/>, to a <c>ReadOnlyNativeSpan&lt;byte&gt;</c>
        /// </summary>
        /// <inheritdoc cref="MemoryMarshal.AsBytes{T}(ReadOnlySpan{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlyNativeSpan<byte> AsBytes<T>(ReadOnlyNativeSpan<T> span) where T : unmanaged => CreateReadOnlyNativeSpan(in As<T, byte>(in span.Head), checked(span.Length * (nuint)Unsafe.SizeOf<T>()));

        /// <inheritdoc cref="MemoryMarshal.Cast{TFrom, TTo}(Span{TFrom})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static NativeSpan<TTo> Cast<TFrom, TTo>(NativeSpan<TFrom> span) where TFrom : unmanaged where TTo : unmanaged
        {
            if (Unsafe.SizeOf<TFrom>() == Unsafe.SizeOf<TTo>())
            {
                return CreateNativeSpan(ref Unsafe.As<TFrom, TTo>(ref span.Head), span.Length);
            }
            if (Unsafe.SizeOf<TTo>() == Unsafe.SizeOf<byte>())
            {
                return CreateNativeSpan(ref Unsafe.As<TFrom, TTo>(ref span.Head), checked(span.Length * (nuint)Unsafe.SizeOf<TFrom>()));
            }
            if (Unsafe.SizeOf<TFrom>() == Unsafe.SizeOf<byte>())
            {
                return CreateNativeSpan(ref Unsafe.As<TFrom, TTo>(ref span.Head), span.Length / (nuint)Unsafe.SizeOf<TTo>());
            }
            if (Unsafe.SizeOf<TFrom>() > Unsafe.SizeOf<TTo>() && Unsafe.SizeOf<TFrom>() % Unsafe.SizeOf<TTo>() == 0)
            {
                return CreateNativeSpan(ref Unsafe.As<TFrom, TTo>(ref span.Head), checked(span.Length * ((nuint)Unsafe.SizeOf<TFrom>() / (nuint)Unsafe.SizeOf<TTo>())));
            }
            if (Unsafe.SizeOf<TTo>() > Unsafe.SizeOf<TFrom>() && Unsafe.SizeOf<TTo>() % Unsafe.SizeOf<TFrom>() == 0)
            {
                return CreateNativeSpan(ref Unsafe.As<TFrom, TTo>(ref span.Head), span.Length / ((nuint)Unsafe.SizeOf<TTo>() / (nuint)Unsafe.SizeOf<TFrom>()));
            }
            var t = Math.BigMul(span.Length, (nuint)Unsafe.SizeOf<TFrom>(), out var low);
            return CreateNativeSpan(ref Unsafe.As<TFrom, TTo>(ref span.Head), MathUtils.BigDivConstant((nuint)t, (nuint)low, (nuint)Unsafe.SizeOf<TTo>()));
        }

        /// <inheritdoc cref="MemoryMarshal.Cast{TFrom, TTo}(ReadOnlySpan{TFrom})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlyNativeSpan<TTo> Cast<TFrom, TTo>(ReadOnlyNativeSpan<TFrom> span) where TFrom : unmanaged where TTo : unmanaged
        {
            if (Unsafe.SizeOf<TFrom>() == Unsafe.SizeOf<TTo>())
            {
                return CreateReadOnlyNativeSpan(in As<TFrom, TTo>(in span.Head), span.Length);
            }
            if (Unsafe.SizeOf<TTo>() == Unsafe.SizeOf<byte>())
            {
                return CreateReadOnlyNativeSpan(in As<TFrom, TTo>(in span.Head), checked(span.Length * (nuint)Unsafe.SizeOf<TFrom>()));
            }
            if (Unsafe.SizeOf<TFrom>() == Unsafe.SizeOf<byte>())
            {
                return CreateReadOnlyNativeSpan(in As<TFrom, TTo>(in span.Head), checked(span.Length / (nuint)Unsafe.SizeOf<TTo>()));
            }
            if (Unsafe.SizeOf<TFrom>() > Unsafe.SizeOf<TTo>() && Unsafe.SizeOf<TFrom>() % Unsafe.SizeOf<TTo>() == 0)
            {
                return CreateReadOnlyNativeSpan(in As<TFrom, TTo>(in span.Head), checked(span.Length / ((nuint)Unsafe.SizeOf<TFrom>() / (nuint)Unsafe.SizeOf<TTo>())));
            }
            var t = Math.BigMul(span.Length, (ulong)Unsafe.SizeOf<TFrom>(), out var low);
            return CreateReadOnlyNativeSpan(in As<TFrom, TTo>(in span.Head), checked(MathUtils.BigDivConstant((nuint)t, (nuint)low, (nuint)Unsafe.SizeOf<TTo>())));
        }
        #endregion

        #region Processor Cache

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static unsafe void Prefetch<T>(ref readonly T pointer)
        {
#if DEBUG
            _ = pointer!;
#else
            if (Sse.IsSupported)
            {
                Sse.Prefetch0(Unsafe.AsPointer(ref Unsafe.AsRef(in pointer)));
            }
#endif
        }

#endregion

        #region Memory Management

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint MaxSizeForType<T>() => nuint.MaxValue / (nuint)Unsafe.SizeOf<T>();

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal static bool ValidateAllocationRequest<T>(nuint length, out nuint lengthInBytes, byte alignmentExponent = 0, ushort alignmentExponentLimit = 256)
        {
            bool isLengthInRange = IsLengthInRange<T>(length, out lengthInBytes);
            return length > 0 && isLengthInRange && alignmentExponent < alignmentExponentLimit;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowInvalidArguments<T>(nuint length, byte alignmentExponent, ushort alignmentExponentLimit)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(length, default);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, MaxSizeForType<T>());
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(alignmentExponent, alignmentExponentLimit);
            checked
            {
                _ = length * (nuint)Unsafe.SizeOf<T>();
            }
            throw new InvalidOperationException("Something went wrong!");
        }

        internal static bool IsLengthInRange<T>(nuint length, out nuint lengthInBytes)
        {
            if (nuint.MaxValue == uint.MaxValue)
            {
                var h = length * (ulong)Unsafe.SizeOf<T>();
                lengthInBytes = (nuint)h;
                return h <= uint.MaxValue;
            }
            else
            {
                var hi = Math.BigMul(length, (ulong)Unsafe.SizeOf<T>(), out var newLength);
                lengthInBytes = (nuint)newLength;
                return hi == 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        internal static unsafe void* AllocateNativeMemoryInternal(nuint lengthInBytes, byte alignmentExponent, bool memoryPressure)
        {
            var alignment = (nuint)1 << alignmentExponent;
            if (memoryPressure)
            {
                AddMemoryPressure(lengthInBytes);
            }
            return alignment > 1 ? NativeMemoryAllocator.AlignedAlloc(lengthInBytes, alignment) : NativeMemoryAllocator.Alloc(lengthInBytes);
        }

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        internal static unsafe void FreeInternal<T>(T* head, nuint length, nuint requestedAlignment)
        {
            RemoveMemoryPressure(length * (nuint)Unsafe.SizeOf<T>());
            if (requestedAlignment > 1)
            {
                NativeMemoryAllocator.AlignedFree(head);
            }
            else
            {
                NativeMemoryAllocator.Free(head);
            }
        }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

        /// <inheritdoc cref="GC.AddMemoryPressure(long)"/>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void AddMemoryPressure(nuint bytesAllocated)
        {
            while (bytesAllocated > 0)
            {
                var max = nuint.Min(bytesAllocated, (nuint)nint.MaxValue);
                GC.AddMemoryPressure((long)max);
                bytesAllocated -= max;
            }
        }

        /// <inheritdoc cref="GC.RemoveMemoryPressure(long)"/>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void RemoveMemoryPressure(nuint bytesAllocated)
        {
            while (bytesAllocated > 0)
            {
                var max = nuint.Min(bytesAllocated, (nuint)nint.MaxValue);
                GC.RemoveMemoryPressure((long)max);
                bytesAllocated -= max;
            }
        }
        #endregion

        #region Unsafe Utils

        /// <inheritdoc cref="Unsafe.Add{T}(ref T, nuint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly T Add<T>(ref readonly T source, nuint elementOffset) => ref Unsafe.Add(ref Unsafe.AsRef(in source), elementOffset);
        /// <inheritdoc cref="Unsafe.As{TFrom, TTo}(ref TFrom)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly TTo As<TFrom, TTo>(ref readonly TFrom source) => ref Unsafe.As<TFrom, TTo>(ref Unsafe.AsRef(in source));

        /// <inheritdoc cref="Unsafe.Subtract{T}(ref T, nuint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly T Subtract<T>(ref readonly T source, nuint elementOffset) => ref Unsafe.Subtract(ref Unsafe.AsRef(in source), elementOffset);

        /// <inheritdoc cref="Unsafe.Add{T}(ref T, nuint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref readonly byte AddElementOffsetReadOnly<T>(ref readonly byte source, nuint elementOffset) => ref Unsafe.As<T, byte>(ref Unsafe.Add(ref Unsafe.As<byte, T>(ref Unsafe.AsRef(in source)), elementOffset));

        /// <inheritdoc cref="Unsafe.Add{T}(ref T, nuint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref byte AddElementOffset<T>(ref byte source, nuint elementOffset) => ref Unsafe.As<T, byte>(ref Unsafe.Add(ref Unsafe.As<byte, T>(ref source), elementOffset));

        /// <inheritdoc cref="Unsafe.ReadUnaligned{T}(ref readonly byte)"/>
        /// <param name="elementOffset">The offset to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadUnaligned<T>(ref readonly byte source, nuint elementOffset) => Unsafe.ReadUnaligned<T>(in AddElementOffsetReadOnly<T>(in source, elementOffset));

        /// <inheritdoc cref="Unsafe.WriteUnaligned{T}(ref byte, T)"/>
        /// <param name="elementOffset">The offset to add.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteUnaligned<T>(ref byte destination, nuint elementOffset, T value) => Unsafe.WriteUnaligned(ref AddElementOffset<T>(ref destination, elementOffset), value);
        #endregion
    }
}
