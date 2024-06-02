using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Allocation
{
    public sealed class NativeMemoryRegionOwner<T> : INativeMemoryOwner<T>
    {
        NativeMemoryRegion<T> region;
        private uint disposedValue;

        private sealed class MemoryManager : NativeMemoryManager<T>
        {
            public override NativeSpan<T> CreateNativeSpan(nuint start, nuint length) => throw new NotImplementedException();
            public override ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length) => throw new NotImplementedException();
            public override NativeSpan<T> GetNativeSpan() => throw new NotImplementedException();
            public override ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start) => throw new NotImplementedException();
            public override MemoryHandle Pin(nuint elementIndex) => throw new NotImplementedException();
            public override void Unpin() => throw new NotImplementedException();
            protected override void Dispose(bool disposing) => throw new NotImplementedException();
        }

        public NativeMemory<T> NativeMemory { get; }
        public Memory<T> Memory { get; }

        private void Dispose(bool disposing)
        {
            if (!AtomicUtils.Exchange(ref disposedValue, true))
            {
                if (disposing || RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    region.NativeSpan.Clear();
                }
                region.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
