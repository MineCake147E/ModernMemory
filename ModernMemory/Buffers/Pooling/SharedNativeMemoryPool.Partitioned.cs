using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Allocation;

namespace ModernMemory.Buffers.Pooling
{
    internal sealed partial class SharedNativeMemoryPool<T>
    {
        internal sealed unsafe class PartitionedArrayMemoryManager<TFixedGenericInlineArray> : NativeMemoryManager<T>, IPartitionedArrayMemoryManager<T>
            where TFixedGenericInlineArray : unmanaged, IFixedGenericInlineArray<uint, TFixedGenericInlineArray>
        {
            private uint partitionSize;
            private NativeMemoryRegion<T> region;
            private TFixedGenericInlineArray occupationFlags;

            /// <summary>
            /// Because of the <see cref="Memory{T}"/>'s limitation, we can't have more than <see cref="int.MaxValue"/> elements for entire allocated bunch of memory.
            /// </summary>
            public static nuint MaxPartitionSize => (nuint)((int.MaxValue ^ (int.MaxValue >> 1)) / (Unsafe.SizeOf<TFixedGenericInlineArray>() * 8));

            public ReadOnlyNativeSpan<uint> OccupationFlags => TFixedGenericInlineArray.AsSpan(ref occupationFlags);

            internal NativeSpan<uint> MutableOccupationFlags => TFixedGenericInlineArray.AsSpan(ref occupationFlags);

            [SkipLocalsInit]
            public PartitionedArrayMemoryManager(uint partitionSize, sbyte alignmentExponent = -1)
            {
                occupationFlags = default;
                if (!BitOperations.IsPow2(Unsafe.SizeOf<T>()))
                {
                    alignmentExponent = 0;
                }
                if (alignmentExponent < 0)
                {
                    alignmentExponent = typeof(T).IsClass ? (sbyte)3 : (sbyte)BitOperations.TrailingZeroCount((partitionSize * (nuint)Unsafe.SizeOf<T>()) | 64);
                }
                else
                {
                    partitionSize = MathUtils.CeilLowerBits(partitionSize, alignmentExponent);
                }
                this.partitionSize = partitionSize;
                NativeSpan<uint> fl = TFixedGenericInlineArray.AsSpan(ref occupationFlags);
                var length = checked((nuint)(uint)(int)(partitionSize * unchecked(fl.Length * sizeof(uint) * 8)));
                region = new(length, (byte)alignmentExponent, true);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void CheckOccupation(nuint startIndex, nuint length = 0)
            {
                var ps = partitionSize;
                ObjectDisposedException.ThrowIf(ps == 0, this);
                (var pos, var rem) = nuint.DivRem(startIndex, ps);
                NativeSpan<uint> occ = TFixedGenericInlineArray.AsSpan(ref occupationFlags);
                NativeMemoryUtils.Prefetch(ref occ.Head);
                var totalLength = region.Length;
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startIndex, totalLength);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(length, ps - rem);
                if (!CheckOccupationBySegmentIndex(pos, occ))
                {
                    throw new AccessViolationException($"The specified region [{startIndex}, {startIndex + length}) has already been released!");
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool CheckOccupationBySegmentIndex(nuint segmentIndex)
                => CheckOccupationBySegmentIndex(segmentIndex, TFixedGenericInlineArray.AsSpan(ref occupationFlags));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool CheckOccupationBySegmentIndex(nuint pos, NativeSpan<uint> occ)
            {
                (var arrIndex, var bitIndex) = Math.DivRem(pos, sizeof(uint) * 8);
                return arrIndex >= occ.Length || (occ[arrIndex] & 1u << (int)bitIndex) > 0;
            }

            [SkipLocalsInit]
            public bool TryAllocate(out nuint allocatedSegmentId, nuint retryCount = 0)
            {
                NativeSpan<uint> ocx = TFixedGenericInlineArray.AsSpan(ref occupationFlags);
                var occ = NativeMemoryUtils.Cast<uint, nuint>(ocx);
                NativeMemoryUtils.Prefetch(ref ocx.Head);
                if (retryCount == 0)
                {
                    retryCount = ocx.Length * 8 * sizeof(uint);
                }
                var remainingRetries = retryCount - 1;
                ObjectDisposedException.ThrowIf(region.IsEmpty, this);
                var res = nuint.MaxValue;
                var i = nuint.MaxValue;
                while (remainingRetries < retryCount && res == nuint.MaxValue)
                {
                    if (++i >= occ.Length)
                    {
                        i -= occ.Length;
                        if (i >= occ.Length)
                        {
                            i %= occ.Length;
                        }
                    }
                    ref var rb = ref occ[i];
                    var b = rb;
                    var cb = ~b;
                    var m = BitOperations.TrailingZeroCount(cb);
                    if (m < Unsafe.SizeOf<nuint>() * 8)
                    {
                        var k = (nuint)1u << m;
                        var ob = Interlocked.CompareExchange(ref rb, k | b, b);
                        if (ob == b)
                        {
                            res = i * (nuint)Unsafe.SizeOf<nuint>() * 8 + (uint)m;
                            break;
                        }
                        m += 1;
                    }
                    remainingRetries -= (uint)m;
                }
                allocatedSegmentId = res;
                return res < nuint.MaxValue;
            }

            public void Return(nuint segmentIndex)
            {
                NativeSpan<uint> occ = TFixedGenericInlineArray.AsSpan(ref occupationFlags);
                (var arrIndex, var bitIndex) = Math.DivRem(segmentIndex, sizeof(uint) * 8);
                var k = ~(1u << (int)bitIndex);
                ref var mask = ref occ[arrIndex];
                if ((Interlocked.And(ref mask, k) & ~k) != 0)
                {
                    return;
                }
                ThrowReleasedSegmentExceptionForRelease(segmentIndex);
            }

            public NativeSpan<T> GetNativeSpanForSegment(nuint segmentIndex) => CheckOccupationBySegmentIndex(segmentIndex)
                    ? GetNativeSpanForSegmentUnsafe(segmentIndex)
                    : ThrowReleasedSegmentExceptionForSpan(segmentIndex);

            public NativeMemory<T> GetNativeMemoryForSegment(nuint segmentIndex) => CheckOccupationBySegmentIndex(segmentIndex)
                    ? GetNativeMemoryForSegmentUnsafe(segmentIndex)
                    : ThrowReleasedSegmentExceptionForNativeMemory(segmentIndex);

            public Memory<T> GetMemoryForSegment(nuint segmentIndex) => CheckOccupationBySegmentIndex(segmentIndex)
                    ? GetMemoryForSegmentUnsafe(segmentIndex)
                    : ThrowReleasedSegmentExceptionForMemory(segmentIndex);

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            private NativeSpan<T> GetNativeSpanForSegmentUnsafe(nuint segmentIndex)
            {
                var ps = partitionSize;
                return region.NativeSpan.Slice(segmentIndex * ps, ps);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            private NativeMemory<T> GetNativeMemoryForSegmentUnsafe(nuint segmentIndex)
            {
                var ps = partitionSize;
                return NativeMemory.Slice(segmentIndex * ps, ps);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            private Memory<T> GetMemoryForSegmentUnsafe(nuint segmentIndex)
            {
                var ps = partitionSize;
                return Memory.Slice(checked((int)(segmentIndex * ps)), checked((int)ps));
            }

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static void ThrowReleasedSegmentExceptionForRelease(nuint segmentIndex)
                => throw new InvalidOperationException($"Attempted to free the free segment #{segmentIndex}!");

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static NativeSpan<T> ThrowReleasedSegmentExceptionForSpan(nuint segmentIndex)
                => throw new AccessViolationException($"The specified segment #{segmentIndex} has already been released!");

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static NativeMemory<T> ThrowReleasedSegmentExceptionForNativeMemory(nuint segmentIndex)
                => throw new AccessViolationException($"The specified segment #{segmentIndex} has already been released!");

            [DoesNotReturn]
            [MethodImpl(MethodImplOptions.NoInlining)]
            private static Memory<T> ThrowReleasedSegmentExceptionForMemory(nuint segmentIndex)
                => throw new AccessViolationException($"The specified segment #{segmentIndex} has already been released!");

            public override NativeSpan<T> CreateNativeSpan(nuint start, nuint length)
            {
                CheckOccupation(start, length);
                return region.NativeSpan.Slice(start, length);
            }

            public override ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length) => CreateNativeSpan(start, length);
            public override NativeSpan<T> GetNativeSpan() => region.NativeSpan;
            public override ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start) => throw new NotSupportedException();
            public override MemoryHandle Pin(nuint elementIndex)
            {
                CheckOccupation(elementIndex);
                return new(region.Head + elementIndex, default, this);
            }
            public override void Unpin() { }
            protected override void Dispose(bool disposing)
            {
                if (Interlocked.Exchange(ref partitionSize, 0) > 0)
                {
                    if (disposing || RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                    {
                        region.NativeSpan.Clear();
                    }
                    region.Dispose();
                }
            }
        }

        internal sealed unsafe class PartitionedArrayMemoryOwner : INativeMemoryOwner<T>
        {
            private nuint start;
            private NativeMemory<T> memory;
            private IPartitionedArrayMemoryManager<T>? memoryManager;
            private readonly SharedNativeMemoryPool<T> pool;

            public PartitionedArrayMemoryOwner(SharedNativeMemoryPool<T> pool)
            {
                ArgumentNullException.ThrowIfNull(pool);
                this.pool = pool;
            }

            public NativeMemory<T> NativeMemory
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    ObjectDisposedException.ThrowIf(start >= nuint.MaxValue, this);
                    return memory;
                }
            }

            public Memory<T> Memory
            {
                get
                {
                    ObjectDisposedException.ThrowIf(memoryManager is null, this);
                    return memoryManager.GetMemoryForSegment(start);
                }
            }

            public NativeSpan<T> Span => NativeMemory.Span;

            internal bool TrySetArray(nuint segmentIndex, IPartitionedArrayMemoryManager<T> memoryManager)
            {
                ArgumentOutOfRangeException.ThrowIfEqual(segmentIndex, nuint.MaxValue);
                var success = Interlocked.CompareExchange(ref start, segmentIndex, nuint.MaxValue) == nuint.MaxValue;
                if (success)
                {
                    this.memoryManager = memoryManager;
                    memory = memoryManager.GetNativeMemoryForSegment(segmentIndex);
                }
                return success;
            }

            private void Dispose(bool disposing)
            {
                var segment = Interlocked.Exchange(ref start, nuint.MaxValue);
                var man = memoryManager;
                if (segment < nuint.MaxValue)
                {
                    memory = default;
                    memoryManager = null;
                    if(man is not null)
                    {
                        if (disposing || RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                        {
                            man.GetNativeSpanForSegment(segment).Clear();
                        }
                        man.Return(segment);
                    }
                    if (disposing)
                    {
                        pool.ReturnOwner(this);
                    }
                }
            }

            ~PartitionedArrayMemoryOwner()
            {
                Dispose(disposing: false);
            }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize (We want `this` to go back to pool)
            public void Dispose() => Dispose(disposing: true);
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        }
    }
}
