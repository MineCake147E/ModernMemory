using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Allocation;

namespace ModernMemory.Buffers.Pooling
{
    internal abstract class AllocatingNativeMemoryPoolBase<T> : NativeMemoryPool<T>
    {
        public override nuint MaxNativeBufferSize => NativeMemoryUtils.MaxSizeForType<T>();

        public override MemoryOwnerContainer<T> Rent(nuint minBufferSize) => minBufferSize switch
        {
            0 => default,
            < int.MaxValue when (int)minBufferSize <= Array.MaxLength => new(CreateOwnerFromArrayPool((int)minBufferSize)),
            _ => new(CreateOwner(new NativeMemoryRegion<T>(minBufferSize)))
        };

        public override MemoryOwnerContainer<T> RentWithDefaultSize() => new(CreateOwnerFromArrayPool(512));

        protected abstract INativeMemoryOwner<T> CreateOwnerFromArrayPool(int minBufferSize);
        protected abstract INativeMemoryOwner<T> CreateOwner(NativeMemoryRegion<T> region);

        protected override void Dispose(bool disposing) { }
    }
    internal sealed class FullAllocatingNativeMemoryPool<T> : AllocatingNativeMemoryPoolBase<T>, IDisposable
    {
        public override nuint MaxNativeBufferSize => NativeMemoryUtils.MaxSizeForType<T>();

        void IDisposable.Dispose() { }

        protected override void Dispose(bool disposing) { }

        protected override INativeMemoryOwner<T> CreateOwnerFromArrayPool(int minBufferSize) => MemoryPool<T>.Shared.Rent(minBufferSize).AsNativeMemoryOwner();
        protected override INativeMemoryOwner<T> CreateOwner(NativeMemoryRegion<T> region) => new NativeMemoryRegionOwner<T>(region);
    }
}
