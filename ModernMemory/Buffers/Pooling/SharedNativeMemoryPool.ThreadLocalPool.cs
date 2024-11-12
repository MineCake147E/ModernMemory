using System.Diagnostics;
using System.Runtime.CompilerServices;

using ModernMemory.Collections.Concurrent;
using ModernMemory.Threading;

namespace ModernMemory.Buffers.Pooling
{
    internal sealed partial class SharedNativeMemoryPool<T>
    {
        private sealed class ThreadLocalPool : NativeMemoryPool<T>
        {
            private DisposableValueSpinLockSlim spinLock;
            private readonly uint id;
            private readonly SharedNativeMemoryPool<T> parent;
            private readonly ThreadLocalSizedArrayPool[] sizedArrayPools;
            private readonly BlockingNativePile<PartitionedArrayMemoryOwner> ownerPool;
            private readonly MemoryArray<PartitionedArrayPool?> partitionedArrayPools;


            private static volatile uint nextId = 0;
            private const uint SharedPoolId = uint.MaxValue;

            public ThreadLocalPool(SharedNativeMemoryPool<T> parent)
            {
                id = Interlocked.Increment(ref nextId) - 1;
                ArgumentNullException.ThrowIfNull(parent);
                this.parent = parent;

            }

            public bool IsShared => id == SharedPoolId;

            public override nuint MaxNativeBufferSize => NativeMemoryUtils.MaxSizeForType<T>();

            public override MemoryOwnerContainer<T> Rent(nuint minBufferSize) => RentInternal(minBufferSize);

            private MemoryOwnerContainer<T> RentInternal(nuint minBufferSize)
            {
                var index = BufferUtils.CalculatePartitionSizeClassIndex(minBufferSize, out var sizeClass);
                
                return SharedAllocatingPool.Rent(sizeClass);
            }

            private bool TryRentByIndex(nuint index, NativeSpan<PartitionedArrayPool?> pap, out MemoryOwnerContainer<T> container)
            {
                if (!ownerPool.TryPopSpinningWhileNotNull(out var owner))
                {
                    parent.sharedOwners.TryTake(out owner);
                }
                owner ??= new(parent);
                var retryCount = nuint.MaxValue;
                var ni = index;
                while (++retryCount < 4 && ni < pap.Length)
                {
                    var pool = pap[ni];
                    if (pool is null)
                    {
                        pap[ni] = pool = new(index);
                    }
                    if (pool.TryRent(owner))
                    {
                        container = new(owner);
                        return true;
                    }
                    ni++;
                }
                if (!ownerPool.TryAdd(owner))
                {
                    parent.sharedOwners.Add(owner);
                }
                container = default;
                return false;
            }

            public override MemoryOwnerContainer<T> RentWithDefaultSize() => Rent(DefaultSize);
            protected override void Dispose(bool disposing) { }
        }

    }
}
