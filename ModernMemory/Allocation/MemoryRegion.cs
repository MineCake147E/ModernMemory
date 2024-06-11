using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Allocation
{
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

    public readonly unsafe struct MemoryRegion<T>
    {
        private readonly T* head;
        private readonly nuint length;

        public MemoryRegion(T* head, nuint length)
        {
            this.head = head;
            this.length = length;
        }

        internal MemoryRegion(NativeMemoryRegion<T> region)
        {
            head = region.Head;
            length = region.Length;
        }

        /// <summary>
        /// The length of this <see cref="NativeMemoryRegion{T}"/>.
        /// </summary>
        public readonly nuint Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => length;
        }

        public ref T this[nuint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get
            {
                if (index >= length)
                {
                    ThrowHelper.ThrowIndexOutOfRangeException();
                }
                return ref head[index];
            }
        }

        /// <summary>
        /// Gets the current alignment in bytes.
        /// </summary>
        public readonly nuint CurrentAlignment => (nuint)1 << BitOperations.TrailingZeroCount((nuint)head);

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> object over the entirety of the current <see cref="NativeMemoryRegion{T}"/>.
        /// </summary>
        public readonly NativeSpan<T> NativeSpan => new(ref Unsafe.AsRef<T>(head), Length);

        /// <summary>
        /// Returns a internalValue that indicates whether the current <see cref="NativeMemoryRegion{T}"/> is empty.
        /// </summary>
        public readonly bool IsEmpty => Length <= 0;

        /// <summary>
        /// Gets the theoretical maximum length of <see cref="NativeMemoryRegion{T}"/>.
        /// </summary>
        public static nuint TheoreticalMaxLength => NativeMemoryUtils.MaxSizeForType<T>();

        public readonly unsafe T* Head => head;
    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
}
