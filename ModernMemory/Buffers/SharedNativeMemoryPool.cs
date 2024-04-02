using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Allocation;

namespace ModernMemory.Buffers
{
    internal sealed class SharedNativeMemoryPool<T> : NativeMemoryPool<T>, IDisposable
    {
        public override nuint MaxNativeBufferSize => NativeMemoryUtils.MaxSizeForType<T>();

        public override INativeMemoryOwner<T> Rent(nuint minBufferSize)
        {
            var shared = MemoryPool<T>.Shared;
            return shared.MaxBufferSize >= 0 && minBufferSize <= (nuint)shared.MaxBufferSize
                ? shared.Rent((int)minBufferSize).AsNativeMemoryOwner()
                : new NativeMemoryRegionMemoryManager<T>(new(minBufferSize));
        }

        public override IMemoryOwner<T> Rent(int minBufferSize = -1) => MemoryPool<T>.Shared.Rent(minBufferSize);
        public override INativeMemoryOwner<T> RentWithDefaultSize() => MemoryPool<T>.Shared.Rent().AsNativeMemoryOwner();
        protected override void Dispose(bool disposing) { } // Shared pool shouldn't do anything in Dispose().

        void IDisposable.Dispose() { }
    }
}
