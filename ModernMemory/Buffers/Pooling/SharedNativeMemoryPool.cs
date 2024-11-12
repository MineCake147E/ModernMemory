using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            var shared = ArrayPool<T>.Shared;
            return new(Array.MaxLength >= 0 && minBufferSize <= (nuint)Array.MaxLength
                ? new SharedPooledArrayMemoryManager<T>(shared.Rent((int)minBufferSize))
                : new NativeMemoryRegionOwner<T>(minBufferSize));
        }

        public override MemoryOwnerContainer<T> RentWithDefaultSize() => new(MemoryPool<T>.Shared.Rent().AsNativeMemoryOwner());

        void IDisposable.Dispose() { }

        private void ReturnOwner(PartitionedArrayMemoryOwner owner) => ArgumentNullException.ThrowIfNull(owner);

        protected override void Dispose(bool disposing) { }

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
