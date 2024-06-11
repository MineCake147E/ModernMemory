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
        MemoryRegionMemoryManager<T>? manager;
        private uint disposedValue = AtomicUtils.GetValue(false);

        public NativeMemoryRegionOwner(nuint size, byte alignmentExponent = 0, bool clear = false)
        {
            var nr = region = new(size, alignmentExponent, clear);
            manager = new(nr);
        }

        internal NativeMemoryRegionOwner(NativeMemoryRegion<T> region)
        {
            this.region = region;
            manager = new(region);
        }

        internal MemoryRegion<T> Region => new(region);

        public NativeSpan<T> Span => region.NativeSpan;

        public NativeMemory<T> NativeMemory => (manager?.NativeMemory) ?? default;
        public Memory<T> Memory => manager?.Memory ?? default;

        private void Dispose(bool disposing)
        {
            if (!AtomicUtils.Exchange(ref disposedValue, true))
            {
                manager?.Destroy();
                manager = null;
                if (disposing || RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    region.NativeSpan.Clear();
                }
                region.Dispose();
                region = default;

            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
