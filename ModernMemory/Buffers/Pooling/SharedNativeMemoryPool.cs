using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ModernMemory.Allocation;
using ModernMemory.Threading;
using System.Numerics;
using System.Collections.Concurrent;

namespace ModernMemory.Buffers.Pooling
{
    internal sealed partial class SharedNativeMemoryPool<T> : NativeMemoryPool<T>, IDisposable
    {
        public override nuint MaxNativeBufferSize => NativeMemoryUtils.MaxSizeForType<T>();

        [ThreadStatic]
        private static ThreadLocalStorage? storage;

        private static ConditionalWeakTable<ThreadLocalStorage, object?> allThreads = new();

        public override MemoryOwnerContainer<T> Rent(nuint minBufferSize)
        {
            if (minBufferSize == 0) return default;
            var shared = MemoryPool<T>.Shared;
            return new(shared.MaxBufferSize >= 0 && minBufferSize <= (nuint)shared.MaxBufferSize
                ? shared.Rent((int)minBufferSize).AsNativeMemoryOwner()
                : new NativeMemoryRegionMemoryManager<T>(new(minBufferSize)));
        }

        public override MemoryOwnerContainer<T> RentWithDefaultSize() => new(MemoryPool<T>.Shared.Rent().AsNativeMemoryOwner());


        void IDisposable.Dispose() { }

        private void ReturnOwner(PartitionedArrayMemoryOwner owner) => ArgumentNullException.ThrowIfNull(owner);

        protected override void Dispose(bool disposing) { }

        private sealed class ThreadLocalStorage
        {
            private Dictionary<uint, PartitionedArrayPool> partitions;
            private ConcurrentBag<PartitionedArrayMemoryOwner> ownerPool;
        }

        private struct PartitionedArrayPool
        {

        }

    }
}
