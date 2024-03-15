using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    public abstract class NativeMemoryManager<T> : MemoryManager<T>, INativeMemoryOwner<T>, INativeSpanFactory<T>
    {
        public virtual NativeMemory<T> NativeMemory => new(this, 0, GetNativeSpan().Length);
        public nuint Length => GetNativeSpan().Length;

        public abstract NativeSpan<T> CreateNativeSpan(nuint start, nuint length);
        public abstract ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length);
        public abstract NativeSpan<T> GetNativeSpan();
        public abstract ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start);
        public ReadOnlyNativeSpan<T> GetReadOnlyNativeSpan() => GetNativeSpan();
        public abstract MemoryHandle Pin(nuint elementIndex);
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)elementIndex, 0u);
            return Pin((nuint)elementIndex);
        }
    }
}
