using ModernMemory.Buffers.Pooling;

namespace ModernMemory.Collections
{
    public sealed partial class NativeRingQueue<T>
    {
        private sealed class BufferSegment
        {
            private nuint readHead;
            private nuint readableGuard;
            private nuint writeHead;
            private uint abandoned;
            private uint isEmpty;
            private nuint writtenCount = 0;
            private NativeMemory<T> memory;
            MemoryOwnerContainer<T> container;

            public BufferSegment()
            {
                abandoned = 0;
                readHead = 0;
                readableGuard = nuint.MaxValue;
                writeHead = 0;
                isEmpty = 1;
            }

            public BufferSegment(MemoryOwnerContainer<T> container) : this()
            {
                this.container = container;
            }

            public bool TryDequeue(out T? item)
            {
                var rm = memory;
                var res = default(T?);
                var success = false;
                var rH = Volatile.Read(ref readHead);
                var rG = Volatile.Read(ref readableGuard);
                if (!rm.IsEmpty && writtenCount > 0)
                {
                    //var contiguousCount = rG - rH;
                    //if (contiguousCount == 0) contiguousCount = rm.Length;
                    var span = rm.Span.Slice(rH);
                    res = span.Head;
                    AdvanceRead(rH, 1, rG, rm.Length);
                }
                item = res;
                return success;
            }

            private void AdvanceRead(nuint readHead, nuint count, nuint rG, nuint length)
            {
                var rH = readHead + count;
                if (rH >= length)
                {
                    rH -= length;
                }
                if (rH == rG)   // the last item has been dequeued
                {
                    if (Volatile.Read(ref abandoned) > 0)
                    {
                        // this segment is now abandoned
                        memory = default;
                        rH = nuint.MaxValue;
                        container.Dispose();
                        container = default;
                    }
                    else if (writeHead == rG)
                    {
                        Interlocked.Or(ref isEmpty, 1);
                    }
                }
                AtomicUtils.Add(ref writtenCount, ~count + 1);
                Volatile.Write(ref readHead, rH);
            }

            public nuint AddAtMost(ReadOnlyNativeSpan<T> items)
            {
                var mem = container.NativeMemory;
                if (items.IsEmpty || mem.IsEmpty || abandoned > 0) return 0;
                var rG = AtomicUtils.Add(ref writeHead, items.Length) - items.Length;   // This lets Interlocked.CompareExchange(ref writeHead, 0, rG) fail in AdvanceRead
                if (rG > ~items.Length)  // addition overflowed
                {
                    Volatile.Write(ref writeHead, nuint.MaxValue);
                    items = items.SliceWhileIfLongerThan(~rG);
                }
                var nwH = rG + items.Length;
                nuint writable = 0;
                if (nwH >= mem.Length)
                {
                    nwH -= mem.Length;
                    writable = mem.Length - rG;
                }
                var rH = Volatile.Read(ref readHead);
                if (rH <= rG)
                {
                    
                }
                if (nwH > rH)  // we need to resize later
                {
                    
                }
                Volatile.Write(ref readableGuard, nwH);
                if (Interlocked.Exchange(ref isEmpty, 0) == 1)
                {

                }
                return writable;
            }
        }
    }
}
