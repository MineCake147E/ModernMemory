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

        protected override void Dispose(bool disposing) { }
    }
}
