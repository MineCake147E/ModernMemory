using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Allocation;
using ModernMemory.Collections.Concurrent;
using ModernMemory.Threading;

namespace ModernMemory.Buffers.Pooling
{
    internal sealed partial class SharedNativeMemoryPool<T> : NativeMemoryPool<T>, IDisposable
    {
        public override nuint MaxNativeBufferSize => NativeMemoryUtils.MaxSizeForType<T>();

        private const uint DefaultSize = 512u;

        [ThreadStatic]
        private static ThreadLocalPool? storage;

        private readonly ThreadLocalPool sharedPool;
        private readonly ConditionalWeakTable<ThreadLocalPool, object?> allThreads = [];
        private readonly ConcurrentBag<IPartitionedArrayMemoryManager<T>> trimmedPartitionedArrayMemoryManagers = [];
        private readonly ConcurrentBag<PartitionedArrayMemoryOwner> sharedOwners = [];
        private readonly ConcurrentBag<ThreadLocalPool> trimmedOwnerPools = [];

        internal SharedNativeMemoryPool()
        {
            
        }

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

        private sealed class ThreadLocalPool : NativeMemoryPool<T>
        {
            private DisposableValueSpinLockSlim spinLock;
            private readonly uint id;
            private readonly SharedNativeMemoryPool<T> parent;
            private readonly BlockingNativePile<PartitionedArrayMemoryOwner> ownerPool;
            private readonly ArrayOwner<PartitionedArrayPool?> partitionedArrayPools;


            private static volatile uint nextId = 0;
            private const uint SharedPoolId = uint.MaxValue;

            public ThreadLocalPool(SharedNativeMemoryPool<T> parent)
            {
                id = Interlocked.Increment(ref nextId) - 1;
                ArgumentNullException.ThrowIfNull(parent);
                this.parent = parent;
                // Due to the growing nature of ownerPool, it might be better using shared pool instead of allocating pool.
                ownerPool = new(NativeMemoryPool<PartitionedArrayMemoryOwner>.Shared, 512);
                var maxPartitionSize = RuntimeFeature.IsDynamicCodeCompiled ? PartitionedArrayMemoryManager<FixedArray4<uint>>.MaxPartitionSize : PartitionedArrayMemoryManager<FixedArray16<uint>>.MaxPartitionSize;
                var maxPartitionSizeClass = BufferUtils.CalculatePartitionSizeClassIndex(maxPartitionSize, out var size);
                if (size > maxPartitionSize && maxPartitionSize > 0) maxPartitionSizeClass--;
                partitionedArrayPools = new(NativeMemoryPool<PartitionedArrayPool?>.SharedAllocatingPool.Rent(maxPartitionSizeClass + 1));
            }

            public bool IsShared => id == SharedPoolId;

            public override nuint MaxNativeBufferSize => NativeMemoryUtils.MaxSizeForType<T>();

            public override MemoryOwnerContainer<T> Rent(nuint minBufferSize) => RentInternal(minBufferSize);

            private MemoryOwnerContainer<T> RentInternal(nuint minBufferSize)
            {
                var index = BufferUtils.CalculatePartitionSizeClassIndex(minBufferSize, out var sizeClass);
                var pap = partitionedArrayPools.Span;
                if (index < partitionedArrayPools.Length)
                {
                    if (TryRentByIndex(index, pap, out var container))
                    {
                        return container;
                    }
                    if (!IsShared)
                    {
                        var shared = parent.sharedPool;
                        if (shared != this) return shared.RentSharedInternal(sizeClass);
                    }
                }
                return SharedAllocatingPool.Rent(sizeClass);
            }

            private MemoryOwnerContainer<T> RentSharedInternal(nuint minBufferSize)
            {
                Debug.Assert(IsShared);
                var index = BufferUtils.CalculatePartitionSizeClassIndex(minBufferSize, out var sizeClass);
                var pap = partitionedArrayPools.Span;
                return index < partitionedArrayPools.Length && TryRentByIndex(index, pap, out var container)
                    ? container
                    : SharedAllocatingPool.Rent(sizeClass);
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

        private sealed class PartitionedArrayPool : IDisposable
        {
            private DisposableValueSpinLockSlim spinLock;
            private readonly nuint partitionSize;
            private BlockingNativePile<PartitionedArrayMemoryManagerBase>? memoryManagers;
            private volatile nuint counter = 0;

            public bool IsInitialized => memoryManagers is { };

            public PartitionedArrayPool(nuint partitionSizeClassIndex)
            {
                var ps = BufferUtils.CalculatePartitionSizeClassFromIndex(partitionSizeClassIndex);
                ValidatePartitionSize(ps);
                partitionSize = ps;
                memoryManagers = new(NativeMemoryPool<PartitionedArrayMemoryManagerBase>.SharedAllocatingPool, 8);
                using var l = memoryManagers.GetAddBufferAtMost(out var buffer, 1);
                InitializeMemoryManagers(buffer, partitionSize);
            }

            public bool TryRent(PartitionedArrayMemoryOwner owner)
            {
                if (spinLock.IsDisposed || memoryManagers is null) return false;
                foreach (var item in memoryManagers.Span)
                {
                    if (item?.TryAllocate(out var id) ?? false)
                    {
                        return owner.TrySetArray(id, item);
                    }
                }
                var c = counter;

                return false;
            }

            private static void ValidatePartitionSize(nuint partitionSize)
            {
                if (RuntimeFeature.IsDynamicCodeCompiled)
                {
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(partitionSize, PartitionedArrayMemoryManager<FixedArray4<uint>>.MaxPartitionSize);
                }
                else
                {
                    // For trimmer friendliness, we don't adjust the size of occupationFlags in NativeAOT builds.
                    ArgumentOutOfRangeException.ThrowIfGreaterThan(partitionSize, PartitionedArrayMemoryManager<FixedArray16<uint>>.MaxPartitionSize);
                }
            }

            private static void InitializeMemoryManagers(NativeSpan<PartitionedArrayMemoryManagerBase> destination, nuint partitionSize)
            {
                if (RuntimeFeature.IsDynamicCodeCompiled)
                {
                    InitializeMemoryManagersDynamic(destination, partitionSize);
                }
                else
                {
                    InitializeMemoryManagersStatic(destination, partitionSize);
                }
            }

            [RequiresDynamicCode($"For the sake of trimmer friendliness, use {nameof(InitializeMemoryManagersStatic)} instead!")]
            private static void InitializeMemoryManagersDynamic(NativeSpan<PartitionedArrayMemoryManagerBase> destination, nuint partitionSize)
            {
                var dst = destination;
                for (nuint i = 0; i < dst.Length; i++)
                {
                    dst[i] = CreateSuitableMemoryManager(partitionSize);
                }
            }

            [RequiresDynamicCode("This path is not trimmer friendly!")]
            private static PartitionedArrayMemoryManagerBase CreateSuitableMemoryManager(nuint partitionSize)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(partitionSize, uint.MaxValue);
                var size = (uint)partitionSize;
                if (TryCreateMemoryManager<FixedArray32<uint>>(size, out var manager) ||
                    TryCreateMemoryManager<FixedArray16<uint>>(size, out manager) ||
                    TryCreateMemoryManager<FixedArray8<uint>>(size, out manager) ||
                    TryCreateMemoryManager<FixedArray4<uint>>(size, out manager))
                {
                    return manager;
                }
                ArgumentOutOfRangeException.ThrowIfGreaterThan(size, PartitionedArrayMemoryManager<FixedArray4<uint>>.MaxPartitionSize);
                throw null!;
            }

            private static bool TryCreateMemoryManager<TFixedGenericInlineArray>(uint partitionSize, [NotNullWhen(true)] out PartitionedArrayMemoryManagerBase? manager)
                where TFixedGenericInlineArray : unmanaged, IFixedGenericInlineArray<uint, TFixedGenericInlineArray>
            {
                if (partitionSize <= PartitionedArrayMemoryManager<TFixedGenericInlineArray>.MaxPartitionSize)
                {
                    manager = new PartitionedArrayMemoryManager<TFixedGenericInlineArray>(partitionSize);
                    return true;
                }
                manager = null;
                return false;
            }

            private static void InitializeMemoryManagersStatic(NativeSpan<PartitionedArrayMemoryManagerBase> destination, nuint partitionSize)
            {
                var dst = destination;
                for (nuint i = 0; i < dst.Length; i++)
                {
                    dst[i] = new PartitionedArrayMemoryManager<FixedArray16<uint>>((uint)partitionSize);
                }
            }

            public void Dispose()
            {
                var a = spinLock.Enter(out var isDisposed);
                if (!isDisposed && a.IsHolding)
                {
                    a.ExitAndDispose();
                    memoryManagers = null;
                }
            }
        }

    }
}
