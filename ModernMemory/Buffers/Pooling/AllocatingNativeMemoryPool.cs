using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Allocation;

namespace ModernMemory.Buffers.Pooling
{
    internal sealed class AllocatingNativeMemoryPool<T> : NativeMemoryPool<T>, IDisposable
    {
        public override nuint MaxNativeBufferSize => NativeMemoryUtils.MaxSizeForType<T>();

        public override MemoryOwnerContainer<T> Rent(nuint minBufferSize)
        {
            var shared = MemoryPool<T>.Shared;
            return new(shared.MaxBufferSize >= 0 && minBufferSize <= (nuint)shared.MaxBufferSize
                ? shared.Rent((int)minBufferSize).AsNativeMemoryOwner()
                : new NativeMemoryRegionMemoryManager<T>(new(minBufferSize)));
        }

        public override MemoryOwnerContainer<T> RentWithDefaultSize() => new(MemoryPool<T>.Shared.Rent().AsNativeMemoryOwner());

        void IDisposable.Dispose() { }

        protected override void Dispose(bool disposing) { }
    }
}
