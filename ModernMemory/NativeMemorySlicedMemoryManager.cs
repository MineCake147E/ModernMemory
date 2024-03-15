using System.Buffers;

using ModernMemory.Buffers;

namespace ModernMemory
{
    internal sealed class NativeMemorySlicedMemoryManager<T>(NativeMemoryManager<T>? manager, nuint start) : MemoryManager<T>
    {
        public override Span<T> GetSpan() => manager is not null ? manager.GetNativeSpan().Slice(start).GetHeadSpan() : default;
        public override MemoryHandle Pin(int elementIndex = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
            return manager is not null ? manager.Pin(start + (nuint)elementIndex) : default;
        }
        public override void Unpin() => manager?.Unpin();
        protected override void Dispose(bool disposing) => manager = null;
    }
}
