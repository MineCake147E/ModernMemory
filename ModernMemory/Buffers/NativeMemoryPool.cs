using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModernMemory.Buffers.Pooling;

namespace ModernMemory.Buffers
{
    public abstract class NativeMemoryPool<T> : IDisposable
    {
        private static readonly SharedNativeMemoryPool<T> SharedPool = new();

        public static NativeMemoryPool<T> Shared => SharedPool;

        public abstract nuint MaxNativeBufferSize { get; }

        /// <summary>
        /// Returns a memory block capable of holding at least 1 element of <typeparamref name="T"/>.
        /// </summary>
        /// <returns>A memory block capable of holding some elements of <typeparamref name="T"/>.</returns>
        public abstract MemoryOwnerContainer<T> RentWithDefaultSize();

        /// <summary>
        /// Returns a memory block capable of holding at least <paramref name="minBufferSize"/> elements of <typeparamref name="T"/>.
        /// </summary>
        /// <param name="minBufferSize">The minimum number of elements of <typeparamref name="T"/> that the memory pool can hold.</param>
        /// <returns>A memory block capable of holding at least <paramref name="minBufferSize"/> elements of <typeparamref name="T"/>.</returns>
        public abstract MemoryOwnerContainer<T> Rent(nuint minBufferSize);

        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
