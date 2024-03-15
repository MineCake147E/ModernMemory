using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModernMemory.Buffers;

namespace ModernMemory
{
    internal sealed class NativeArrayNativeMemoryManager<T>(NativeArray<T>? array) : NativeMemoryManager<T>
    {
        public override NativeSpan<T> CreateNativeSpan(nuint start, nuint length) => GetNativeSpan().Slice(start, length);
        public override ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length) => GetNativeSpan().Slice(start, length);
        public override NativeSpan<T> GetNativeSpan() => array is not null ? array.NativeSpan : default;
        public override ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start) => new NativeMemorySlicedMemoryManager<T>(this, start).Memory;
        public override Span<T> GetSpan() => array is not null ? array.NativeSpan.GetHeadSpan() : default;
        public override MemoryHandle Pin(nuint elementIndex) => array is not null ? array.Pin(elementIndex) : default;
        public override void Unpin() => array?.Unpin();
        protected override void Dispose(bool disposing) => array = null;
    }
}
