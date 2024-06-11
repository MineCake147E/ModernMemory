using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers.Pooling;
using ModernMemory.Collections.Concurrent;
using ModernMemory.Threading;

namespace ModernMemory.Collections
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed partial class NativeRingQueue<T>
    {
        DisposableValueSpinLockSlim spinLock;
        private nuint readHead = 0;
        private nuint readableGuard = nuint.MaxValue;
        private nuint readCount = 0;
        private nuint writeHead = 0;
        private nuint writtenCount = 0;
        private NativeMemory<T> readMemory;
        private MemoryResizer<T> resizer;
        private BlockingNativeQueue<MemoryOwnerContainer<T>> readOnlyContainers;
        /*
        private static nuint GetCapacity(nuint length, nuint readHead, nuint writeHead)
        {

        }*/

        public bool TryDequeue(out T? item)
        {
            var rm = readMemory;
            var res = default(T?);
            var success = false;
            var rH = Volatile.Read(ref readHead);
            var rG = Volatile.Read(ref readableGuard);
            if (!rm.IsEmpty && rG < nuint.MaxValue)
            {
                //var contiguousCount = rG - rH;
                //if (contiguousCount == 0) contiguousCount = rm.Length;
                var span = rm.Span.Slice(rH);
                res = span.Head;
                if (++rH >= rm.Length)
                {
                    rH = 0;
                }
                if (rH == rG)   // the last item has been dequeued
                {
                    if (readOnlyContainers.TryDequeue(out var container) && container.HasOwner)
                    {
                        // Resize has been in progress
                        using var k = spinLock.Enter();
                        container.Dispose();
                        readMemory = default;
                    }
                    rH = 0;
                }
                Volatile.Write(ref readHead, rH);
            }
            item = res;
            return success;
        }

        public void Add(ReadOnlyNativeSpan<T> items)
        {
            var mem = resizer.Memory;
            var rG = Volatile.Read(ref readableGuard);
            if (rG == nuint.MaxValue) rG = 0;
            var wH = rG + items.Length;
            if (wH >= mem.Length)
            {
                wH -= mem.Length;
                if (wH > readHead)  // we need to resize
                {
                    wH = items.Length;

                }
            }
            Volatile.Write(ref writeHead, wH);
            AtomicUtils.Add(ref writtenCount, items.Length);
        }
    }
}
