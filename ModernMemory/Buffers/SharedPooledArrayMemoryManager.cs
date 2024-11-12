using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    internal sealed class SharedPooledArrayMemoryManager<T>(T[]? array) : NativeMemoryManager<T>
    {
        public override NativeSpan<T> CreateNativeSpan(nuint start, nuint length) => array.AsNativeSpan().Slice(start, length);
        public override ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length) => array.AsNativeSpan().Slice(start, length);
        public override ref T GetReferenceAt(nuint start = 0U) => ref GetNativeSpan()[start];
        public override NativeSpan<T> GetNativeSpan() => array.AsNativeSpan();
        public override ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start) => throw new NotSupportedException();
        public override unsafe MemoryHandle Pin(nuint elementIndex)
        {
            if (array is null) return default;
            var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            return new(Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(array)), handle);
        }

        public override string? ToString() => array?.ToString();

        public override void Unpin() { }
        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref array, null) is { } a)
            {
                ArrayPool<T>.Shared.Return(a, RuntimeHelpers.IsReferenceOrContainsReferences<T>());
            }
        }
    }
}
