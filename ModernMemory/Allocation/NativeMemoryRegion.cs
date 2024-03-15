﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Allocation
{
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeMemoryRegion<T> : IDisposable
    {
#pragma warning disable IDE0032 // Use auto property (head needs to be placed on top of struct)
        private T* head;
#pragma warning restore IDE0032 // Use auto property
        private nuint length;
        private readonly byte alignmentExponent;
        /// <summary>
        /// The length of this <see cref="NativeMemoryRegion{T}"/>.
        /// </summary>
        public readonly nuint Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => length;
        }

        /// <summary>
        /// Gets the current alignment in bytes.
        /// </summary>
        public readonly nuint CurrentAlignment => (nuint)1 << BitOperations.TrailingZeroCount((nuint)head);

        /// <summary>
        /// Gets the initially desired alignment in bytes.
        /// </summary>
        public readonly nuint RequestedAlignment => (nuint)1 << alignmentExponent;

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> object over the entirety of the current <see cref="NativeMemoryRegion{T}"/>.
        /// </summary>
        public NativeSpan<T> NativeSpan => new(ref *head, Length);

        /// <summary>
        /// Returns a internalValue that indicates whether the current <see cref="NativeMemoryRegion{T}"/> is empty.
        /// </summary>
        public readonly bool IsEmpty => Length <= 0;

        /// <summary>
        /// Gets the theoretical maximum length of <see cref="NativeMemoryRegion{T}"/>.
        /// </summary>
        public static nuint TheoreticalMaxLength => NativeMemoryUtils.MaxSizeForType<T>();

        public readonly unsafe T* Head => head;

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeMemoryRegion{T}"/> struct.
        /// </summary>
        /// <param name="length">The length.</param>
        /// <param name="alignmentExponent">The number of zeros to be at lowest significant bits.</param>
        /// <param name="fillWithZero">The internalValue which indicates whether the <see cref="NativeMemoryRegion{T}"/> should perform <see cref="Clear"/> after allocating.</param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public NativeMemoryRegion(nuint length, byte alignmentExponent = 0, bool fillWithZero = false)
        {
            var alignmentExponentLimit = (ushort)(Unsafe.SizeOf<nuint>() * 8);
            if (!NativeMemoryUtils.ValidateAllocationRequest<T>(length, out var lengthInBytes, alignmentExponent, alignmentExponentLimit))
            {
                NativeMemoryUtils.ThrowInvalidArguments<T>(length, alignmentExponent, alignmentExponentLimit);
                return;
            }
            this.alignmentExponent = alignmentExponent;
            head = (T*)NativeMemoryUtils.AllocateNativeMemoryInternal(lengthInBytes, alignmentExponent, true);
            this.length = length;

            if (fillWithZero)
            {
                NativeSpan.Clear();
            }
        }

        public void Dispose()
        {
            var len = length;
            var h = head;
            var requestedAlignment = RequestedAlignment;
            if (h is not null && len > 0 && Interlocked.CompareExchange(ref length, 0, len) == len)
            {
                NativeMemoryUtils.FreeInternal(h, len, requestedAlignment);
                head = null;
            }
        }
    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
}
