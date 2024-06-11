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
    internal sealed class MemoryRegionMemoryManager<T>(MemoryRegion<T> region) : NativeMemoryManager<T>
    {
        public override nuint Length => region.Length;
        public override NativeSpan<T> CreateNativeSpan(nuint start, nuint length) => region.NativeSpan.Slice(start, length);
        public override ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length) => region.NativeSpan.Slice(start, length);
        public override ref T GetReferenceAt(nuint start = 0) => ref region[start];
        public override NativeSpan<T> GetNativeSpan() => region.NativeSpan;
        public override ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start) => new NativeMemorySlicedMemoryManager<T>(this, start).Memory;
        public override Span<T> GetSpan() => region.NativeSpan.GetHeadSpan();
        public override unsafe MemoryHandle Pin(nuint elementIndex) => new(Unsafe.AsPointer(ref region[elementIndex]), default, this);
        public override void Unpin() { }
        protected override void Dispose(bool disposing) => region = default;
        internal void Destroy() => region = default;
    }
}
