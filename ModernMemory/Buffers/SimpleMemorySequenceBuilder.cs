using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections;
using ModernMemory.Threading;

namespace ModernMemory.Buffers
{
    public sealed class SimpleMemorySequenceBuilder<T> : IReadOnlyMemorySequenceBuilder<T>
    {
        private ReadOnlyMemorySequence<T> cache;
        private uint cacheInvalidated = AtomicUtils.GetValue(true);
        private NativeQueue<ReadOnlyNativeMemory<T>>? appendQueue;
        private OverwritableNativeQueue<ReadOnlySequenceSegment<T>> readableSegments;
        private MemoryResizer<ReadOnlyNativeMemory<T>> buildBuffer;
        private ValueSpinLockSlim buildLock;
        private ValueSpinLockSlim appendLock;
        private nuint slicedCacheSegments = 0;
        private nuint slicedCacheIndex = 0;
        private nuint lastRunningIndex = 0;

        public nuint CurrentElementCount { get; }
        public nuint CurrentSegmentCount => appendQueue?.Count ?? 0;

        public void AdvanceTo(SlimSequencePosition consumed) => AdvanceTo(consumed, consumed);
        public nuint AdvanceTo(SlimSequencePosition consumed, SlimSequencePosition examined)
        {
            var res = cache.GetSize(consumed, examined);
            slicedCacheSegments += consumed.SegmentPosition;
            slicedCacheIndex = consumed.Index;
            cache = cache.Slice(consumed);
            return res;
        }
        public nuint Append(ReadOnlyNativeMemory<T> memory) => Append([memory]);
        public nuint Append(ReadOnlyNativeSpan<ReadOnlyNativeMemory<T>> memories)
        {
            nuint res = 0;
            if (!memories.IsEmpty)
            {
                ObjectDisposedException.ThrowIf(appendQueue is null, this);
                _ = AtomicUtils.Exchange(ref cacheInvalidated, true);
                using var acquiredLock = appendLock.Enter();
                appendQueue.Add(memories);
            }
            return res;
        }
        public ReadOnlyMemorySequence<T> Build()
        {
            var result = cache;
            using var acquiredBuildLock = buildLock.Enter();
            if (AtomicUtils.LoadValue(in cacheInvalidated))
            {
                ObjectDisposedException.ThrowIf(appendQueue is null, this);
                nuint size;
                using (var acquiredAppendLock = appendLock.Enter())
                {
                    var span = appendQueue.Span;
                    size = span.Length;
                    buildBuffer.Resize(span);
                    appendQueue.DiscardHead(span.Length);
                }
                var memories = buildBuffer.NativeMemory.Span.SliceWhile(size);
                readableSegments.DiscardHead(slicedCacheSegments);
                slicedCacheSegments = 0;
                var dst = readableSegments.GetNativeSpan(memories.Length);
                var success = ReadOnlyMemorySequence<T>.ConstructSegments(ref lastRunningIndex, memories, dst);
                if (!success)
                {
                    var span = readableSegments.Span;
                    ref var firstSegment = ref span.Head;
                    var memory = firstSegment.Memory.Slice(slicedCacheIndex);
                    firstSegment = new(memory, 0);
                    var runningIndex = ReadOnlyMemorySequence<T>.ReconstructSegments(span);
                    dst = readableSegments.GetNativeSpan(memories.Length);
                    lastRunningIndex = ReadOnlyMemorySequence<T>.ConstructSegments(runningIndex, memories, dst);
                }
                slicedCacheIndex = 0;
                readableSegments.Advance(memories.Length);
                cache = result = new((ReadOnlyNativeMemory<ReadOnlySequenceSegment<T>>)readableSegments.Memory);
            }
            return result;
        }
        public ReadOnlySequenceSlim<T> BuildSlim() => Build();
        public void Clear() => throw new NotImplementedException();

        private void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref appendQueue, null) is { } q)
            {
                _ = disposing;
                q.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
