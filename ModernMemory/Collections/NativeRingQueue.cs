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
    public sealed partial class NativeRingQueue<T> : IDisposable
    {
        private DisposableValueSpinLockSlim spinLock;
        private nuint readHead;
        private nuint readableTail;
        private nuint writeHead;
        internal MemoryResizer<T> resizer;

        internal bool IsDisposed => spinLock.IsDisposed;


    }

    public sealed partial class NativeRingQueue<T>
    {
        private void Dispose(bool disposing)
        {
            if (!spinLock.IsDisposed && spinLock.TryDispose())
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~NativeRingQueue()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
