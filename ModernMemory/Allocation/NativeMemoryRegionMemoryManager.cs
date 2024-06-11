using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Allocation
{
    internal class NativeMemoryRegionMemoryManagerBase<T>(NativeMemoryRegion<T> region) : NativeMemoryManager<T>
    {
        NativeMemoryRegion<T> region = region;

        public override NativeSpan<T> CreateNativeSpan(nuint start, nuint length) => region.NativeSpan.Slice(start, length);
        public override ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length) => region.NativeSpan.Slice(start, length);
        public override ref T GetReferenceAt(nuint start = 0) => ref region.NativeSpan[start];
        public override NativeSpan<T> GetNativeSpan() => region.NativeSpan;
        public override ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start) => new NativeMemorySlicedMemoryManager<T>(this, start).Memory;
        public override Span<T> GetSpan() => region.NativeSpan.GetHeadSpan();
        public override unsafe MemoryHandle Pin(nuint elementIndex)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(elementIndex, Length);
            return new(region.Head + elementIndex, default, this);
        }
        public override void Unpin() { }
        protected override void Dispose(bool disposing) => region.Dispose();
    }
    internal sealed class NativeMemoryRegionMemoryManager<T>(NativeMemoryRegion<T> region) : NativeMemoryRegionMemoryManagerBase<T>(region)
    {
    }
}
