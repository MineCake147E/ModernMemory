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
    internal sealed class MemoryManagerWrapper<T>(MemoryManager<T> memoryManager) : NativeMemoryManager<T>
    {
        private MemoryManager<T>? memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));

        private MemoryManager<T> MemoryManager
        {
            get
            {
                ObjectDisposedException.ThrowIf(memoryManager == null, this);
                return memoryManager;
            }
        }

        public override NativeSpan<T> CreateNativeSpan(nuint start, nuint length) => GetNativeSpan().Slice(start, length);
        public override ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length) => GetReadOnlyNativeSpan().Slice(start, length);
        public override ref T GetReferenceAt(nuint start = 0U) => ref GetNativeSpan()[start];

        public override NativeSpan<T> GetNativeSpan() => MemoryManager.GetSpan();
        public override ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start)
        {
            var memory = MemoryManager.Memory;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, (nuint)memory.Length);
            return memory.Slice((int)start);
        }
        public override Span<T> GetSpan() => MemoryManager.GetSpan();
        public override MemoryHandle Pin(nuint elementIndex)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(elementIndex, (nuint)int.MaxValue);
            return Pin((int)elementIndex);
        }
        public override MemoryHandle Pin(int elementIndex = 0) => MemoryManager.Pin(elementIndex);
        public override void Unpin() => MemoryManager.Unpin();
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                (memoryManager as IDisposable)?.Dispose();
                memoryManager = null;
            }
        }
    }
}
