using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    public abstract class NativeMemoryPool<T> : MemoryPool<T>
    {
        private static readonly SharedNativeMemoryPool<T> SharedPool = new();
        public static new NativeMemoryPool<T> Shared => SharedPool;

        public override int MaxBufferSize => (int)nuint.Min(int.MaxValue, MaxNativeBufferSize);

        public override IMemoryOwner<T> Rent(int minBufferSize = -1) => minBufferSize < 0 ? RentWithDefaultSize() : Rent((nuint)minBufferSize);

        public abstract nuint MaxNativeBufferSize { get; }

        /// <summary>
        /// Returns a memory block capable of holding at least 1 element of <typeparamref name="T"/>.
        /// </summary>
        /// <returns>A memory block capable of holding some elements of <typeparamref name="T"/>.</returns>
        public abstract INativeMemoryOwner<T> RentWithDefaultSize();

        /// <summary>
        /// Returns a memory block capable of holding at least <paramref name="minBufferSize"/> elements of <typeparamref name="T"/>.
        /// </summary>
        /// <param name="minBufferSize">The minimum number of elements of <typeparamref name="T"/> that the memory pool can hold.</param>
        /// <returns>A memory block capable of holding at least <paramref name="minBufferSize"/> elements of <typeparamref name="T"/>.</returns>
        public abstract INativeMemoryOwner<T> Rent(nuint minBufferSize);
    }
}
